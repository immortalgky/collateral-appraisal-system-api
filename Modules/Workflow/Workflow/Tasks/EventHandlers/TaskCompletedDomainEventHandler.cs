using MassTransit;
using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Workflow.Workflow.Events;

namespace Workflow.Tasks.EventHandlers;

public class TaskCompletedDomainEventHandler(
    IAssignmentRepository assignmentRepository,
    IPublishEndpoint publishEndpoint,
    IIntegrationEventOutbox outbox,
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

        // Capture before potential reassign (Reassign() clears WorkingBy and changes AssignedTo)
        var wasStarted = pendingTask.WorkingBy is not null;
        var wasOverdue = pendingTask.SlaStatus == "Breached";
        var assignedAt = pendingTask.AssignedAt;
        var originalAssignedTo = pendingTask.AssignedTo;

        // Implicit assignment: if pool task completed without claiming, assign to the completer
        if (pendingTask.AssignedType == "2" && !string.IsNullOrEmpty(notification.CompletedBy))
        {
            pendingTask.Reassign(notification.CompletedBy, "1");
            logger.LogInformation(
                "Implicit assignment: pool task {TaskId} assigned to completer {CompletedBy}",
                pendingTask.Id, notification.CompletedBy);
        }

        var completedBy = notification.CompletedBy ?? pendingTask.AssignedTo;

        var completedTask = CompletedTask.CreateFromPendingTask(
            pendingTask, notification.ActionTaken, notification.CompletedAt, notification.Remark,
            notification.Movement);

        await assignmentRepository.AddCompletedTaskAsync(completedTask, cancellationToken);
        await assignmentRepository.RemovePendingTaskAsync(pendingTask, cancellationToken);

        logger.LogInformation(
            "Moved PendingTask {TaskId} to CompletedTask for CorrelationId {CorrelationId}",
            pendingTask.Id, notification.CorrelationId);

        await publishEndpoint.Publish(new TaskCompletedIntegrationEvent
        {
            CorrelationId = notification.CorrelationId,
            TaskName = notification.TaskName,
            ActionTaken = notification.ActionTaken,
            CompletedBy = completedBy,
            WorkflowInstanceName = notification.WorkflowInstanceName,
            AppraisalNumber = notification.AppraisalNumber,
            AssignedAt = assignedAt,
            WasStarted = wasStarted,
            WasOverdue = wasOverdue,
            OriginalAssignedTo = originalAssignedTo,
            Movement = notification.Movement
        }, cancellationToken);

        if (string.Equals(notification.Movement, "C", StringComparison.OrdinalIgnoreCase))
        {
            // Per-voter completions from ApprovalActivity use TaskName "{activity}:{voter}".
            // Only publish cancel for the aggregated completion (no ':'), so N voters don't
            // emit N duplicate events that InboxGuard would just short-circuit.
            if (notification.TaskName.Contains(':'))
            {
                logger.LogDebug(
                    "Skipping AppraisalCancelIntegrationEvent for per-voter task {TaskName}",
                    notification.TaskName);
            }
            else if (string.IsNullOrWhiteSpace(completedBy))
            {
                logger.LogError(
                    "Cannot publish AppraisalCancelIntegrationEvent: CompletedBy and AssignedTo both missing. CorrelationId {CorrelationId} TaskName {TaskName}",
                    notification.CorrelationId, notification.TaskName);
            }
            else
            {
                // Outbox-delivered for guaranteed publish (matches committee approval pattern).
                outbox.Publish(new AppraisalCancelIntegrationEvent
                {
                    CorrelationId = notification.CorrelationId,
                    CancelledBy = completedBy,
                    CancelledAt = notification.CompletedAt,
                    CancelReason = notification.Remark
                }, notification.CorrelationId.ToString());
            }
        }
    }
}
