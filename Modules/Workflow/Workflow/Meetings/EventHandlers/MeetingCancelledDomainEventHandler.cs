using Workflow.Meetings.Domain.Events;
using Workflow.Meetings.ReadModels;

namespace Workflow.Meetings.EventHandlers;

/// <summary>
/// When a meeting is cancelled, return each queue item back to Queued status so
/// the Meeting Secretary can assign the appraisal to another meeting. The
/// workflow stays paused.
/// </summary>
public class MeetingCancelledDomainEventHandler(
    WorkflowDbContext dbContext,
    ILogger<MeetingCancelledDomainEventHandler> logger)
    : INotificationHandler<MeetingCancelledDomainEvent>
{
    public async Task Handle(MeetingCancelledDomainEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Meeting {MeetingId} cancelled — returning appraisal {AppraisalId} to the meeting queue",
            notification.MeetingId, notification.AppraisalId);

        var queueItem = await dbContext.MeetingQueueItems
            .FirstOrDefaultAsync(q =>
                q.AppraisalId == notification.AppraisalId &&
                q.WorkflowInstanceId == notification.WorkflowInstanceId &&
                q.Status == MeetingQueueItemStatus.Assigned, cancellationToken);

        if (queueItem is not null)
            queueItem.ReturnToQueue();
    }
}
