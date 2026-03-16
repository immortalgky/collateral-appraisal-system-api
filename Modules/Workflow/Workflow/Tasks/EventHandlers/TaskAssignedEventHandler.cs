using Shared.Messaging.Values;
using Workflow.Workflow.Events;

namespace Workflow.Tasks.EventHandlers;

public class TaskAssignedEventHandler(
    IAssignmentRepository assignmentRepository,
    ILogger<TaskAssignedEventHandler> logger
) : INotificationHandler<TaskAssignedEvent>
{
    public async Task Handle(TaskAssignedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Handling TaskAssignedEvent for CorrelationId {CorrelationId}, TaskName {TaskName}, AssignedTo {AssignedTo}",
            notification.CorrelationId, notification.TaskName, notification.AssignedTo);

        // Check for existing PendingTask for this correlation (previous workflow step)
        var existingTask = await assignmentRepository.GetPendingTaskByCorrelationIdAsync(
            notification.CorrelationId, cancellationToken);

        if (existingTask is not null)
        {
            // Move previous step's task to CompletedTask as "Reassigned"
            var completedTask = CompletedTask.CreateFromPendingTask(existingTask, "Reassigned", DateTime.UtcNow);
            await assignmentRepository.AddCompletedTaskAsync(completedTask, cancellationToken);
            await assignmentRepository.RemovePendingTaskAsync(existingTask, cancellationToken);
        }

        // Create new PendingTask for the current workflow step
        var pendingTask = PendingTask.Create(
            notification.CorrelationId,
            notification.TaskName,
            notification.AssignedTo,
            notification.AssignedType,
            notification.AssignedAt);

        await assignmentRepository.AddTaskAsync(pendingTask, cancellationToken);

        logger.LogInformation(
            "Created PendingTask {TaskId} for CorrelationId {CorrelationId} at step {TaskName}",
            pendingTask.Id, notification.CorrelationId, notification.TaskName);
    }
}
