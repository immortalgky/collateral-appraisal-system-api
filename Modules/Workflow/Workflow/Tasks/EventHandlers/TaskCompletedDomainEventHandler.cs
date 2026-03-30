using MassTransit;
using Shared.Messaging.Events;
using Workflow.Workflow.Events;

namespace Workflow.Tasks.EventHandlers;

public class TaskCompletedDomainEventHandler(
    IAssignmentRepository assignmentRepository,
    IPublishEndpoint publishEndpoint,
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

        // Implicit assignment: if pool task completed without claiming, assign to the completer
        if (pendingTask.AssignedType == "2" && !string.IsNullOrEmpty(notification.CompletedBy))
        {
            pendingTask.Reassign(notification.CompletedBy, "1", DateTime.UtcNow);
            logger.LogInformation(
                "Implicit assignment: pool task {TaskId} assigned to completer {CompletedBy}",
                pendingTask.Id, notification.CompletedBy);
        }

        var completedBy = notification.CompletedBy ?? pendingTask.AssignedTo;

        var completedTask = CompletedTask.CreateFromPendingTask(
            pendingTask, notification.ActionTaken, notification.CompletedAt);

        await assignmentRepository.AddCompletedTaskAsync(completedTask, cancellationToken);
        await assignmentRepository.RemovePendingTaskAsync(pendingTask, cancellationToken);

        logger.LogInformation(
            "Moved PendingTask {TaskId} to CompletedTask for CorrelationId {CorrelationId}",
            pendingTask.Id, notification.CorrelationId);

        // Publish integration event so Notification module can send real-time notifications
        await publishEndpoint.Publish(new TaskCompletedIntegrationEvent
        {
            CorrelationId = notification.CorrelationId,
            TaskName = notification.TaskName,
            ActionTaken = notification.ActionTaken,
            CompletedBy = completedBy,
            WorkflowInstanceName = notification.WorkflowInstanceName
        }, cancellationToken);
    }
}
