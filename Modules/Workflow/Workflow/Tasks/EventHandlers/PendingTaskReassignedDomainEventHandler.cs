using Workflow.Workflow.Events;

namespace Workflow.Tasks.EventHandlers;

public class PendingTaskReassignedDomainEventHandler(
    ILogger<PendingTaskReassignedDomainEventHandler> logger
) : INotificationHandler<PendingTaskReassignedDomainEvent>
{
    public Task Handle(PendingTaskReassignedDomainEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Task {TaskId} reassigned from {Previous} to {New} in workflow {WorkflowInstanceId}",
            notification.TaskId,
            notification.PreviousAssignedTo,
            notification.NewAssignedTo,
            notification.WorkflowInstanceId);

        return Task.CompletedTask;
    }
}
