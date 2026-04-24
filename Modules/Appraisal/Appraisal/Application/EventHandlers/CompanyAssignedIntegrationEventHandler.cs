using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Quotations;
using Appraisal.Infrastructure;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Appraisal.Application.EventHandlers;

public class CompanyAssignedIntegrationEventHandler(
    IAppraisalRepository appraisalRepository,
    IQuotationRepository quotationRepository,
    IAppraisalUnitOfWork unitOfWork,
    AppraisalDbContext dbContext,
    IAssignmentFeeService feeService,
    ILogger<CompanyAssignedIntegrationEventHandler> logger,
    InboxGuard<AppraisalDbContext> inboxGuard
) : IConsumer<CompanyAssignedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<CompanyAssignedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;
        var ct = context.CancellationToken;

        logger.LogInformation(
            "Integration Event received: {IntegrationEvent} for AppraisalId: {AppraisalId}, CompanyId: {CompanyId}",
            nameof(CompanyAssignedIntegrationEvent), message.AppraisalId, message.CompanyId);

        var appraisal = await appraisalRepository.GetByIdWithAllDataAsync(message.AppraisalId, ct);

        if (appraisal is null)
        {
            logger.LogWarning(
                "Appraisal {AppraisalId} not found for company assignment", message.AppraisalId);
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
            return;
        }

        var isQuotationPath = string.Equals(message.AssignmentMethod, "Quotation", StringComparison.OrdinalIgnoreCase);

        var assignment = appraisal.Assignments
            .Where(a => a.AssignmentStatus.Code != "Rejected" && a.AssignmentStatus.Code != "Cancelled")
            .OrderByDescending(a => a.AssignedAt)
            .FirstOrDefault();

        if (assignment is null)
        {
            logger.LogWarning(
                "No active assignment found for Appraisal {AppraisalId}", message.AppraisalId);
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
            return;
        }

        // v4 Quotation path: idempotency guard — if the assignment already records this
        // company as Quotation winner, skip re-processing to avoid duplicate fee creation.
        if (isQuotationPath &&
            string.Equals(assignment.AssigneeCompanyId, message.CompanyId.ToString(), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(assignment.AssignmentMethod, "Quotation", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation(
                "Quotation winner already recorded for AppraisalId={AppraisalId}, CompanyId={CompanyId}. Skipping.",
                message.AppraisalId, message.CompanyId);
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
            return;
        }

        if (isQuotationPath)
        {
            // v4: Quotation path — record winner without promoting to Assigned status.
            // The assignment stays Pending while the quotation child workflow executes.
            // The downstream quotation workflow (ext-appraisal-assignment) will promote
            // the status when the company begins the appraisal work.
            assignment.RecordQuotationWinner(message.CompanyId, "System");

            // ── Link the winning QuotationRequest and create the fee ──────────────
            // C4: pass the per-appraisal fee from the event so HandleQuotationAssignmentAsync
            // does not fall back to the aggregate total price.
            await HandleQuotationAssignmentAsync(appraisal.Id, message.CompanyId, assignment, message.Fee, ct);
        }
        else
        {
            // Non-quotation path (Manual / Auto): promote to Assigned immediately.
            // Preserve internal-followup fields: Assign() defaults them to null, so without
            // passing them back we'd clobber values set by InternalFollowupAssignedIntegrationEventHandler
            // if that event was processed first (consumer delivery order is not guaranteed).
            assignment.Assign(
                assignmentType: "External",
                assigneeCompanyId: message.CompanyId.ToString(),
                assignmentMethod: message.AssignmentMethod,
                internalAppraiserId: assignment.InternalAppraiserId,
                internalFollowupMethod: assignment.InternalFollowupAssignmentMethod,
                assignedBy: "System");

            await feeService.EnsureAssignmentFeeItemsAsync(
                appraisalId: message.AppraisalId,
                assignmentId: assignment.Id,
                source: new AssignmentFeeSource.TierBased(),
                ct: ct);
        }

        await appraisalRepository.UpdateAsync(appraisal, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);

        logger.LogInformation(
            "Updated AppraisalAssignment for AppraisalId {AppraisalId}: CompanyId={CompanyId}, Method={Method}",
            message.AppraisalId, message.CompanyId, message.AssignmentMethod);
    }

    /// <summary>
    /// When the assignment method is Quotation, find the Finalized QuotationRequest
    /// for this appraisal, link its Id on the assignment, and materialise the fee via IAssignmentFeeService.
    /// Guards: QuotationRequest must be in Finalized status and winning company must match.
    /// Fee materialisation is idempotent — safe to retry on duplicate event delivery.
    /// </summary>
    private async Task HandleQuotationAssignmentAsync(
        Guid appraisalId,
        Guid winningCompanyId,
        AppraisalAssignment assignment,
        decimal? eventFee,
        CancellationToken ct)
    {
        // Look up the Finalized QuotationRequest linked to this appraisal.
        // There should be exactly one non-cancelled RFQ per appraisal at any time.
        var quotationRequest = await quotationRepository.GetFinalizedByAppraisalIdAsync(appraisalId, ct);

        if (quotationRequest is null)
        {
            logger.LogWarning(
                "No Finalized QuotationRequest found for AppraisalId={AppraisalId} during Quotation assignment. " +
                "Assignment will be recorded without QuotationRequestId.",
                appraisalId);
            return;
        }

        // Link the quotation on the assignment
        assignment.SetQuotationRequestId(quotationRequest.Id);

        // Determine the final fee amount.
        // Priority: per-appraisal fee carried on the event (populated by QuotationFinalizedIntegrationEventHandler
        // fan-out) → negotiated price on the winning quotation → original total quoted price.
        // The event-level fee is the most accurate because it comes from CompanyQuotationItem.QuotedPrice
        // for the specific appraisal, avoiding the "full quotation total applied to every appraisal" bug (C4).
        decimal? finalFeeAmount = null;

        if (eventFee.HasValue && eventFee.Value > 0m)
        {
            finalFeeAmount = eventFee.Value;
        }
        else
        {
            var winningQuotation = quotationRequest.Quotations
                .FirstOrDefault(q => q.CompanyId == winningCompanyId && q.IsWinner);
            finalFeeAmount = winningQuotation?.CurrentNegotiatedPrice
                             ?? winningQuotation?.TotalQuotedPrice;
        }

        if (finalFeeAmount is null or <= 0m)
        {
            logger.LogWarning(
                "Cannot materialise AppraisalFee for QuotationRequest {QuotationRequestId}: winning quotation has no usable price. " +
                "Assignment is linked but fee materialisation is skipped.",
                quotationRequest.Id);
            return;
        }

        await feeService.EnsureAssignmentFeeItemsAsync(
            appraisalId: appraisalId,
            assignmentId: assignment.Id,
            source: new AssignmentFeeSource.Quotation(finalFeeAmount.Value, quotationRequest.Id),
            ct: ct);
    }
}
