using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Shared.Time;
using Workflow.Data;
using Workflow.Meetings.Configuration;
using Workflow.Meetings.ReadModels;
using Workflow.Workflow.Repositories;

namespace Workflow.Meetings.EventHandlers;

/// <summary>
/// Automatically enqueues an <see cref="AppraisalAcknowledgementQueueItem"/> when
/// an appraisal is approved by a committee whose code maps to an acknowledgement group.
/// Covers both sub-committee and committee approvals routed through <c>ApprovalActivity</c>.
/// Committees not in <see cref="AcknowledgementGroupSettings"/> are skipped silently
/// (not every committee approval requires acknowledgement).
/// </summary>
public class AppraisalApprovedAckIntegrationEventConsumer(
    ILogger<AppraisalApprovedAckIntegrationEventConsumer> logger,
    WorkflowDbContext dbContext,
    IWorkflowUnitOfWork unitOfWork,
    IOptions<AcknowledgementGroupSettings> settings,
    IDateTimeProvider dateTimeProvider,
    InboxGuard<WorkflowDbContext> inboxGuard)
    : IConsumer<AppraisalApprovedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<AppraisalApprovedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var msg = context.Message;
        var groupMap = settings.Value.AcknowledgementGroupByCommitteeCode;

        if (!groupMap.TryGetValue(msg.CommitteeCode, out var ackGroup))
        {
            // Not a committee that produces acknowledgements — skip silently.
            logger.LogDebug(
                "Committee code {CommitteeCode} has no acknowledgement group mapping; skipping enqueue for AppraisalId {AppraisalId}",
                msg.CommitteeCode, msg.AppraisalId);
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
            return;
        }

        // Domain-level idempotency — skip if an active queue item already exists
        // for this appraisal + committee (PendingAcknowledgement or Included).
        var alreadyEnqueued = await dbContext.AppraisalAcknowledgementQueueItems
            .AnyAsync(
                q => q.AppraisalId == msg.AppraisalId
                     && q.CommitteeId == msg.CommitteeId
                     && q.Status != AcknowledgementStatus.Acknowledged,
                context.CancellationToken);

        if (alreadyEnqueued)
        {
            logger.LogInformation(
                "Acknowledgement already enqueued for AppraisalId {AppraisalId} CommitteeId {CommitteeId}; skipping",
                msg.AppraisalId, msg.CommitteeId);
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
            return;
        }

        var item = AppraisalAcknowledgementQueueItem.Create(
            appraisalId: msg.AppraisalId,
            appraisalNo: msg.AppraisalNo,
            appraisalDecisionId: null,
            committeeId: msg.CommitteeId,
            committeeCode: msg.CommitteeCode,
            acknowledgementGroup: ackGroup,
            now: dateTimeProvider.ApplicationNow);

        dbContext.AppraisalAcknowledgementQueueItems.Add(item);
        await unitOfWork.SaveChangesAsync(context.CancellationToken);
        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);

        logger.LogInformation(
            "Enqueued AppraisalAcknowledgementQueueItem {ItemId} for AppraisalId {AppraisalId} CommitteeCode {CommitteeCode} Group {Group}",
            item.Id, msg.AppraisalId, msg.CommitteeCode, ackGroup);
    }
}
