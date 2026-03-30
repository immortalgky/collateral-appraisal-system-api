using MassTransit;
using Shared.Messaging.Events;
using Workflow.Workflow.Events;

namespace Workflow.Tasks.EventHandlers;

public class TaskAssignedEventHandler(
    IAssignmentRepository assignmentRepository,
    IPublishEndpoint publishEndpoint,
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

        // Use CompletedBy from event (reliable), fall back to DB lookup (may be stale)
        string? previousAssignee = notification.CompletedBy ?? existingTask?.AssignedTo;

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
            notification.AssignedAt,
            notification.WorkflowInstanceId,
            notification.ActivityId,
            notification.DueAt,
            notification.TaskDescription);

        await assignmentRepository.AddTaskAsync(pendingTask, cancellationToken);

        logger.LogInformation(
            "Created PendingTask {TaskId} for CorrelationId {CorrelationId} at step {TaskName}",
            pendingTask.Id, notification.CorrelationId, notification.TaskName);

        // Publish integration event so Notification module can send real-time notifications
        await publishEndpoint.Publish(new TaskAssignedIntegrationEvent
        {
            CorrelationId = notification.CorrelationId,
            TaskName = notification.TaskName,
            AssignedTo = notification.AssignedTo,
            AssignedType = notification.AssignedType,
            CompletedBy = previousAssignee,
            StartedBy = notification.StartedBy,
            WorkflowInstanceName = notification.WorkflowInstanceName
        }, cancellationToken);
    }
}
