using Workflow.Meetings.Domain.Events;
using Workflow.Meetings.ReadModels;

namespace Workflow.Meetings.EventHandlers;

/// <summary>
/// When a meeting is cancelled, this handler fires exactly once (regardless of decision-item count):
/// - Returns each Assigned <see cref="MeetingQueueItem"/> back to Queued in a single batch.
/// - Returns any <see cref="AppraisalAcknowledgementQueueItem"/> that was Included in this meeting
///   back to PendingAcknowledgement so it can be picked up by the next cut-off.
/// The workflow stays paused; the event carries the full list of affected decision items.
/// </summary>
public class MeetingCancelledDomainEventHandler(
    WorkflowDbContext dbContext,
    IWorkflowUnitOfWork unitOfWork,
    ILogger<MeetingCancelledDomainEventHandler> logger)
    : INotificationHandler<MeetingCancelledDomainEvent>
{
    public async Task Handle(MeetingCancelledDomainEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Meeting {MeetingId} cancelled — returning {DecisionItemCount} decision queue item(s) to queue",
            notification.MeetingId, notification.DecisionItems.Count);

        // ----- Return decision queue items to Queued (batch) -----
        if (notification.DecisionItems.Count > 0)
        {
            var appraisalIds = notification.DecisionItems.Select(d => d.AppraisalId).ToList();
            var workflowInstanceIds = notification.DecisionItems.Select(d => d.WorkflowInstanceId).ToList();

            var queueItems = await dbContext.MeetingQueueItems
                .Where(q =>
                    appraisalIds.Contains(q.AppraisalId) &&
                    workflowInstanceIds.Contains(q.WorkflowInstanceId) &&
                    q.Status == MeetingQueueItemStatus.Assigned)
                .ToListAsync(cancellationToken);

            foreach (var qi in queueItems)
                qi.ReturnToQueue();

            logger.LogInformation(
                "Meeting {MeetingId}: returned {Count} MeetingQueueItem(s) to Queued",
                notification.MeetingId, queueItems.Count);
        }

        // ----- Always: return included ack items back to PendingAcknowledgement -----
        var ackItems = await dbContext.AppraisalAcknowledgementQueueItems
            .Where(a => a.MeetingId == notification.MeetingId && a.Status == AcknowledgementStatus.Included)
            .ToListAsync(cancellationToken);

        foreach (var ai in ackItems)
            ai.ReturnToPending();

        if (ackItems.Count > 0)
        {
            logger.LogInformation(
                "Meeting {MeetingId}: returned {Count} AppraisalAcknowledgementQueueItem(s) to PendingAcknowledgement",
                notification.MeetingId, ackItems.Count);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
