using Workflow.Workflow.Events;
using Workflow.Workflow.Services;

namespace Workflow.Tasks.EventHandlers;

public class PendingTaskReassignedDomainEventHandler(
    IWorkflowNotificationService notificationService,
    ILogger<PendingTaskReassignedDomainEventHandler> logger
) : INotificationHandler<PendingTaskReassignedDomainEvent>
{
    public async Task Handle(PendingTaskReassignedDomainEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Task {TaskId} reassigned from {Previous} to {New} in workflow {WorkflowInstanceId}",
            notification.TaskId,
            notification.PreviousAssignedTo,
            notification.NewAssignedTo,
            notification.WorkflowInstanceId);

        // Notify the new assignee via the existing user-task notification channel
        await notificationService.NotifyUserTaskAssigned(
            notification.NewAssignedTo,
            notification.WorkflowInstanceId,
            "Task Reassigned",
            notification.ActivityId);
    }
}
