using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Appraisal.Application.EventHandlers;

/// <summary>
/// Stamps CompletedAt + ApprovedByCommittee on the Appraisal aggregate.
/// Published by ApprovalActivity.ResumeActivityAsync when the final decision
/// (after any decisionConditions remap) resolves to "approve".
/// </summary>
public class AppraisalApprovedByCommitteeIntegrationEventHandler(
    ILogger<AppraisalApprovedByCommitteeIntegrationEventHandler> logger,
    IAppraisalRepository appraisalRepository,
    IAppraisalUnitOfWork unitOfWork,
    InboxGuard<AppraisalDbContext> inboxGuard)
    : IConsumer<AppraisalApprovedByCommitteeIntegrationEvent>
{
    public async Task Consume(ConsumeContext<AppraisalApprovedByCommitteeIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;
        var ct = context.CancellationToken;

        logger.LogInformation(
            "Integration Event received: {IntegrationEvent} for AppraisalId: {AppraisalId} CommitteeCode: {CommitteeCode}",
            nameof(AppraisalApprovedByCommitteeIntegrationEvent),
            message.AppraisalId,
            message.CommitteeCode);

        try
        {
            var appraisal = await appraisalRepository.GetByIdAsync(message.AppraisalId, ct);

            if (appraisal is null)
            {
                logger.LogWarning(
                    "Appraisal {AppraisalId} not found when handling {IntegrationEvent}",
                    message.AppraisalId,
                    nameof(AppraisalApprovedByCommitteeIntegrationEvent));
                return;
            }

            appraisal.MarkApprovedByCommittee(message.CommitteeCode, message.ApprovedAt);

            await unitOfWork.SaveChangesAsync(ct);
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);

            logger.LogInformation(
                "Successfully stamped committee approval for AppraisalId {AppraisalId} CommitteeCode {CommitteeCode}",
                message.AppraisalId,
                message.CommitteeCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error processing {IntegrationEvent} for AppraisalId: {AppraisalId}",
                nameof(AppraisalApprovedByCommitteeIntegrationEvent),
                message.AppraisalId);

            throw;
        }
    }
}
