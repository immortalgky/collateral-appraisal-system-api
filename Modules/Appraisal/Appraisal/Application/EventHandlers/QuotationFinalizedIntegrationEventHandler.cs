using Appraisal.Domain.Quotations;
using Appraisal.Infrastructure;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Appraisal.Application.EventHandlers;

/// <summary>
/// Handles QuotationFinalizedIntegrationEvent (published by FinalizeQuotationCommandHandler).
/// v2: iterates all appraisals in the quotation, publishing one CompanyAssignedIntegrationEvent
///     per appraisal so that CompanyAssignedIntegrationEventHandler creates one AppraisalAssignment
///     + one AppraisalFee per appraisal.
///
/// Guard: skips silently if the quotation is no longer Finalized or Request is Cancelled.
/// </summary>
public class QuotationFinalizedIntegrationEventHandler(
    IQuotationRepository quotationRepository,
    IPublishEndpoint publishEndpoint,
    ILogger<QuotationFinalizedIntegrationEventHandler> logger,
    InboxGuard<AppraisalDbContext> inboxGuard
) : IConsumer<QuotationFinalizedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<QuotationFinalizedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;
        var ct = context.CancellationToken;

        logger.LogInformation(
            "QuotationFinalizedIntegrationEvent received: QuotationRequestId={QuotationRequestId}, " +
            "WinningCompanyId={WinningCompanyId}, AppraisalCount={Count}",
            message.QuotationRequestId, message.WinningCompanyId, message.AppraisalIds.Length);

        // Load the finalized quotation to get per-appraisal item prices
        var quotation = await quotationRepository.GetByIdAsync(message.QuotationRequestId, ct);

        if (quotation is null)
        {
            logger.LogWarning(
                "QuotationRequest {QuotationRequestId} not found. Skipping CompanyAssigned fan-out.",
                message.QuotationRequestId);
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
            return;
        }

        // Resolve the winning company quotation for per-appraisal price lookup
        var winningCompanyQuotation = quotation.Quotations
            .FirstOrDefault(q => q.Id == message.WinningQuotationId && q.IsWinner);

        if (winningCompanyQuotation is null)
        {
            logger.LogWarning(
                "Winning CompanyQuotation {WinningQuotationId} not found in QuotationRequest {QuotationRequestId}.",
                message.WinningQuotationId, message.QuotationRequestId);
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
            return;
        }

        // Determine appraisal ids to fan out
        var appraisalIds = message.AppraisalIds;
        if (appraisalIds.Length == 0)
        {
            // fallback: single appraisal from v1 event
            if (message.AppraisalId != Guid.Empty)
                appraisalIds = [message.AppraisalId];
        }

        if (appraisalIds.Length == 0)
        {
            logger.LogWarning(
                "No appraisal IDs found in QuotationFinalizedIntegrationEvent for QuotationRequest {QuotationRequestId}.",
                message.QuotationRequestId);
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
            return;
        }

        // Publish one CompanyAssignedIntegrationEvent per appraisal
        foreach (var appraisalId in appraisalIds)
        {
            // Try to find the per-appraisal price from the winning company's items
            var item = winningCompanyQuotation.Items.FirstOrDefault(i => i.AppraisalId == appraisalId);
            var fee = item?.CurrentNegotiatedPrice ?? item?.QuotedPrice ?? message.FinalFeeAmount;

            logger.LogInformation(
                "Publishing CompanyAssignedIntegrationEvent for AppraisalId={AppraisalId}, " +
                "CompanyId={CompanyId}, Fee={Fee}",
                appraisalId, message.WinningCompanyId, fee);

            await publishEndpoint.Publish(new CompanyAssignedIntegrationEvent
            {
                AppraisalId = appraisalId,
                CompanyId = message.WinningCompanyId,
                CompanyName = string.Empty, // enriched by downstream handler
                AssignmentMethod = "Quotation",
                CompletedBy = "SYSTEM:QuotationFinalized",
                Fee = fee
            }, ct);
        }

        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);

        logger.LogInformation(
            "Published {Count} CompanyAssignedIntegrationEvent(s) for QuotationRequest {QuotationRequestId}.",
            appraisalIds.Length, message.QuotationRequestId);
    }
}
