using Shared.Messaging.Values;
using Workflow.Workflow.Events;

namespace Workflow.Tasks.EventHandlers;

public class TaskCompletedDomainEventHandler(
    IAssignmentRepository assignmentRepository,
    ILogger<TaskCompletedDomainEventHandler> logger
) : INotificationHandler<TaskCompletedDomainEvent>
{
    public async Task Handle(TaskCompletedDomainEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Handling TaskCompletedDomainEvent for CorrelationId {CorrelationId}, TaskName {TaskName}, Action {ActionTaken}",
            notification.CorrelationId, notification.TaskName, notification.ActionTaken);

        var pendingTask = await assignmentRepository.GetPendingTaskAsync(
            notification.CorrelationId, notification.TaskName, cancellationToken);

        if (pendingTask is null)
        {
            logger.LogWarning(
                "No PendingTask found for CorrelationId {CorrelationId} and TaskName {TaskName}",
                notification.CorrelationId, notification.TaskName);
            return;
        }

        var completedTask = CompletedTask.CreateFromPendingTask(
            pendingTask, notification.ActionTaken, notification.CompletedAt);

        await assignmentRepository.AddCompletedTaskAsync(completedTask, cancellationToken);
        await assignmentRepository.RemovePendingTaskAsync(pendingTask, cancellationToken);

        logger.LogInformation(
            "Moved PendingTask {TaskId} to CompletedTask for CorrelationId {CorrelationId}",
            pendingTask.Id, notification.CorrelationId);
    }
}
