using MassTransit;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Appraisal.Application.EventHandlers;

/// <summary>
/// Maps workflow activity transitions to Appraisal status changes.
/// Each DestinationActivityId is mapped to an <see cref="AppraisalStatus"/> that reflects
/// where the workflow is now parked — mirroring the semantics of the CASE expression in
/// vw_AppraisalList.sql so that the persisted status stays in sync with the workflow graph.
///
/// Terminal transitions (DestinationActivityId == null) are intentional no-ops here;
/// MarkApprovedByCommittee / Cancel own those transitions via their dedicated events.
/// </summary>
public class WorkflowTransitionedIntegrationEventHandler(
    ILogger<WorkflowTransitionedIntegrationEventHandler> logger,
    IAppraisalRepository appraisalRepository,
    IAppraisalUnitOfWork unitOfWork,
    InboxGuard<AppraisalDbContext> inboxGuard)
    : IConsumer<WorkflowTransitionedIntegrationEvent>
{
    // Tier 1: status depends on whether an active assignment exists.
    // No active assignment → Submitted (RM/checker maker-checker stage).
    // Active assignment exists (routed back from in-progress work) → InProgress.
    private static readonly HashSet<string> AssignmentDependentActivities =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "appraisal-initiation",
            "appraisal-initiation-check",
        };

    // Tier 2: fixed status regardless of assignment state.
    private static readonly IReadOnlyDictionary<string, AppraisalStatus> ActivityStatusMap =
        new Dictionary<string, AppraisalStatus>(StringComparer.OrdinalIgnoreCase)
        {
            ["appraisal-assignment"]        = AppraisalStatus.Pending,
            ["ext-appraisal-assignment"]    = AppraisalStatus.InProgress,
            ["ext-appraisal-execution"]     = AppraisalStatus.InProgress,
            ["ext-appraisal-check"]         = AppraisalStatus.InProgress,
            ["ext-appraisal-verification"]  = AppraisalStatus.InProgress,
            ["int-appraisal-execution"]     = AppraisalStatus.InProgress,
            ["int-appraisal-check"]         = AppraisalStatus.UnderReview,
            ["int-appraisal-verification"]  = AppraisalStatus.UnderReview,
            ["appraisal-book-verification"] = AppraisalStatus.UnderReview,
            ["pending-meeting"]             = AppraisalStatus.UnderReview,
            ["pending-approval"]            = AppraisalStatus.UnderReview,
        };

    public async Task Consume(ConsumeContext<WorkflowTransitionedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;
        var ct = context.CancellationToken;

        // Terminal workflow completion or non-appraisal workflow — nothing to do.
        if (message.AppraisalId is null || message.DestinationActivityId is null)
        {
            logger.LogDebug(
                "WorkflowTransitionedIntegrationEvent skipped: AppraisalId={AppraisalId} DestinationActivityId={DestinationActivityId}",
                message.AppraisalId, message.DestinationActivityId);
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
            return;
        }

        logger.LogInformation(
            "Integration Event received: {IntegrationEvent} for AppraisalId: {AppraisalId} Source: {Source} Destination: {Destination}",
            nameof(WorkflowTransitionedIntegrationEvent),
            message.AppraisalId,
            message.SourceActivityId,
            message.DestinationActivityId);

        try
        {
            // Tier 3: transient/unmapped activities — no status change.
            var isTier1 = AssignmentDependentActivities.Contains(message.DestinationActivityId);
            ActivityStatusMap.TryGetValue(message.DestinationActivityId, out var staticTarget);
            var isTier2 = !isTier1 && staticTarget is not null;

            if (!isTier1 && !isTier2)
            {
                logger.LogDebug(
                    "WorkflowTransitionedIntegrationEvent: no status mapping for activity {DestinationActivityId}, skipping",
                    message.DestinationActivityId);
                await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
                return;
            }

            var appraisal = isTier1
                ? await appraisalRepository.GetByIdWithAllDataAsync(message.AppraisalId.Value, ct)
                : await appraisalRepository.GetByIdAsync(message.AppraisalId.Value, ct);

            var target = isTier1
                ? (appraisal?.HasActiveAssignment == true ? AppraisalStatus.InProgress : AppraisalStatus.Submitted)
                : staticTarget!;

            if (appraisal is null)
            {
                logger.LogWarning(
                    "Appraisal {AppraisalId} not found when handling {IntegrationEvent}",
                    message.AppraisalId,
                    nameof(WorkflowTransitionedIntegrationEvent));
                await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
                return;
            }

            appraisal.SyncStatusFromWorkflow(target);

            await unitOfWork.SaveChangesAsync(ct);
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);

            logger.LogInformation(
                "Successfully synced AppraisalId {AppraisalId} status to {Status} via workflow transition to {DestinationActivityId}",
                message.AppraisalId,
                target.Code,
                message.DestinationActivityId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error processing {IntegrationEvent} for AppraisalId: {AppraisalId}",
                nameof(WorkflowTransitionedIntegrationEvent),
                message.AppraisalId);

            throw;
        }
    }
}
