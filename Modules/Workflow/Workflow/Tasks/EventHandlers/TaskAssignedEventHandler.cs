using MassTransit;
using Shared.Messaging.Events;
using Workflow.Workflow.Events;

namespace Workflow.Tasks.EventHandlers;

public class TaskAssignedEventHandler(
    IAssignmentRepository assignmentRepository,
    IPublishEndpoint publishEndpoint,
    IDateTimeProvider dateTimeProvider,
    ILogger<TaskAssignedEventHandler> logger
) : INotificationHandler<TaskAssignedEvent>
{
    public async Task Handle(TaskAssignedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Handling TaskAssignedEvent for CorrelationId {CorrelationId}, TaskName {TaskName}, AssignedTo {AssignedTo}",
            notification.CorrelationId, notification.TaskName, notification.AssignedTo);

        // Check for existing PendingTask from the previous step of THIS workflow instance.
        // Scope by WorkflowInstanceId (not CorrelationId) because multiple workflows can share
        // the same CorrelationId — e.g. a document-followup child workflow inherits the parent
        // appraisal's requestId — and we must not archive a sibling workflow's pending task.
        var existingTask = await assignmentRepository.GetPendingTaskByWorkflowInstanceIdAsync(
            notification.WorkflowInstanceId, cancellationToken);

        // Use CompletedBy from event (reliable), fall back to DB lookup (may be stale)
        string? previousAssignee = notification.CompletedBy ?? existingTask?.AssignedTo;

        if (existingTask is not null)
        {
            // Move previous step's task to CompletedTask as "Reassigned"
            var completedTask = CompletedTask.CreateFromPendingTask(existingTask, "Reassigned", dateTimeProvider.ApplicationNow);
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
            notification.TaskDescription,
            notification.Movement);

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
            WorkflowInstanceName = notification.WorkflowInstanceName,
            AppraisalNumber = notification.AppraisalNumber,
            AssignedAt = notification.AssignedAt
        }, cancellationToken);
    }
}
