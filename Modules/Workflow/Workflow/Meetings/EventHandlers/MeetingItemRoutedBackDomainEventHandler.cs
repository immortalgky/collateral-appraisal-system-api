using Workflow.Meetings.Domain;
using Workflow.Meetings.Domain.Events;
using Workflow.Workflow.Services;

namespace Workflow.Meetings.EventHandlers;

/// <summary>
/// Resumes the workflow for a routed-back meeting decision item.
/// Called after the secretary executes <c>RouteBackItem</c> on the Meeting aggregate.
///
/// Resume input carries:
/// - <c>meetingId</c>: the meeting Guid.
/// - <c>meetingOutcome</c>: "routeback" (see <see cref="MeetingOutcomes.RouteBack"/>).
/// - <c>routeBackReason</c>: the secretary's reason for routing back.
/// - <c>completedBy</c>: the secretary who routed back this item.
/// </summary>
public class MeetingItemRoutedBackDomainEventHandler(
    IWorkflowService workflowService,
    ILogger<MeetingItemRoutedBackDomainEventHandler> logger)
    : INotificationHandler<MeetingItemRoutedBackDomainEvent>
{
    public async Task Handle(MeetingItemRoutedBackDomainEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "MeetingItem routed back — resuming workflow {WorkflowInstanceId} at activity {ActivityId} " +
            "for appraisal {AppraisalId} in meeting {MeetingId}. Reason: {Reason}",
            notification.WorkflowInstanceId, notification.ActivityId,
            notification.AppraisalId, notification.MeetingId, notification.Reason);

        await workflowService.ResumeWorkflowAsync(
            workflowInstanceId: notification.WorkflowInstanceId,
            activityId: notification.ActivityId,
            completedBy: notification.RoutedBackBy,
            input: new Dictionary<string, object>
            {
                ["meetingId"] = notification.MeetingId,
                ["meetingOutcome"] = MeetingOutcomes.RouteBack,
                ["routeBackReason"] = notification.Reason,
                ["completedBy"] = notification.RoutedBackBy
            },
            cancellationToken: cancellationToken);
    }
}
