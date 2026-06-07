using Workflow.Workflow.Events;

namespace Workflow.Tasks.EventHandlers;

/// <summary>
/// Closes out the leftover per-member approval tasks when an approval round resolves before every
/// member has voted (Quorum mode early decision, or an immediate route_back). Each remaining open
/// <see cref="PendingTask"/> for the activity is archived to <see cref="CompletedTask"/> and removed,
/// mirroring <c>WorkflowService.CancelWorkflowAsync</c> but scoped to a single (instance, activity).
/// </summary>
public class ApprovalRoundClosedEventHandler(
    IAssignmentRepository assignmentRepository,
    ILogger<ApprovalRoundClosedEventHandler> logger
) : INotificationHandler<ApprovalRoundClosedEvent>
{
    public async Task Handle(ApprovalRoundClosedEvent notification, CancellationToken cancellationToken)
    {
        var leftovers = await assignmentRepository.GetFanOutPendingTasksAsync(
            notification.WorkflowInstanceId, notification.ActivityId, cancellationToken);

        if (leftovers.Count == 0)
            return;

        foreach (var task in leftovers)
        {
            var completed = CompletedTask.CreateFromPendingTask(
                task, notification.Reason, notification.ClosedAt);
            await assignmentRepository.AddCompletedTaskAsync(completed, cancellationToken);
            await assignmentRepository.RemovePendingTaskAsync(task, cancellationToken);
        }

        logger.LogInformation(
            "ApprovalRoundClosed: archived {Count} unvoted approval task(s) for instance {InstanceId} activity {ActivityId} ({Reason})",
            leftovers.Count, notification.WorkflowInstanceId, notification.ActivityId, notification.Reason);
    }
}
