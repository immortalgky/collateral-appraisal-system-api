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
/// - <c>meetingMemberOverrides</c>: this meeting's roster as <c>{ userId, role }</c> pairs —
///   consumed by the downstream ApprovalActivity as its member list, replacing the members it
///   would otherwise resolve from the committee. Sending the roster (not just user ids) is what
///   makes per-meeting add/remove/position edits actually govern who votes; the role carries the
///   meeting position so committee <c>RoleRequired</c> conditions still evaluate.
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
                ["meetingMemberOverrides"] = notification.Members
                    .Select(m => new { userId = m.UserId, role = m.Role })
                    .ToArray(),
                ["completedBy"] = notification.ReleasedBy
            },
            cancellationToken: cancellationToken);
    }
}
