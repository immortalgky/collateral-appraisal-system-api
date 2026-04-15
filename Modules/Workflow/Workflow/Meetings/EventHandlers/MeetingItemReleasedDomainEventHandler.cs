using Workflow.Meetings.Domain;
using Workflow.Meetings.Domain.Events;
using Workflow.Workflow.Services;

namespace Workflow.Meetings.EventHandlers;

/// <summary>
/// Resumes the workflow for a released meeting decision item.
/// Called after the secretary executes <c>ReleaseItem</c> on the Meeting aggregate.
///
/// Resume input carries:
/// - <c>meetingId</c>: the meeting Guid.
/// - <c>meetingOutcome</c>: "released" (see <see cref="MeetingOutcomes.Released"/>).
/// - <c>meetingMemberUserIds</c>: user IDs of all meeting members — consumed by the downstream
///   ApprovalActivity as its approver list.
/// - <c>completedBy</c>: the secretary who released this item.
/// </summary>
public class MeetingItemReleasedDomainEventHandler(
    IWorkflowService workflowService,
    ILogger<MeetingItemReleasedDomainEventHandler> logger)
    : INotificationHandler<MeetingItemReleasedDomainEvent>
{
    public async Task Handle(MeetingItemReleasedDomainEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "MeetingItem released — resuming workflow {WorkflowInstanceId} at activity {ActivityId} " +
            "for appraisal {AppraisalId} in meeting {MeetingId}",
            notification.WorkflowInstanceId, notification.ActivityId,
            notification.AppraisalId, notification.MeetingId);

        await workflowService.ResumeWorkflowAsync(
            workflowInstanceId: notification.WorkflowInstanceId,
            activityId: notification.ActivityId,
            completedBy: notification.ReleasedBy,
            input: new Dictionary<string, object>
            {
                ["meetingId"] = notification.MeetingId,
                ["meetingOutcome"] = MeetingOutcomes.Released,
                ["meetingMemberUserIds"] = notification.MemberUserIds.ToArray(),
                ["completedBy"] = notification.ReleasedBy
            },
            cancellationToken: cancellationToken);
    }
}
