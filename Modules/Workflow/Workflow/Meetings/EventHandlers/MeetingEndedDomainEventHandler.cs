using Workflow.Meetings.Domain.Events;
using Workflow.Meetings.ReadModels;
using Workflow.Workflow.Engine;

namespace Workflow.Meetings.EventHandlers;

/// <summary>
/// When a meeting ends, resume each paused workflow whose MeetingActivity was
/// waiting on this meeting item, and release the corresponding queue rows.
/// </summary>
public class MeetingEndedDomainEventHandler(
    IWorkflowEngine workflowEngine,
    WorkflowDbContext dbContext,
    ILogger<MeetingEndedDomainEventHandler> logger)
    : INotificationHandler<MeetingEndedDomainEvent>
{
    public async Task Handle(MeetingEndedDomainEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Meeting {MeetingId} ended — resuming workflow {WorkflowInstanceId} activity {ActivityId} for appraisal {AppraisalId}",
            notification.MeetingId, notification.WorkflowInstanceId, notification.ActivityId, notification.AppraisalId);

        // Release the queue row (it was Assigned while on the meeting)
        var queueItem = await dbContext.MeetingQueueItems
            .FirstOrDefaultAsync(q =>
                q.AppraisalId == notification.AppraisalId &&
                q.WorkflowInstanceId == notification.WorkflowInstanceId &&
                q.Status == MeetingQueueItemStatus.Assigned, cancellationToken);

        if (queueItem is not null)
            queueItem.Release();

        var resumeInput = new Dictionary<string, object>
        {
            ["meetingId"] = notification.MeetingId,
            ["meetingOutcome"] = "ended",
            ["completedBy"] = "MeetingSecretary"
        };

        await workflowEngine.ResumeWorkflowAsync(
            notification.WorkflowInstanceId,
            notification.ActivityId,
            completedBy: "MeetingSecretary",
            input: resumeInput,
            cancellationToken: cancellationToken);
    }
}
