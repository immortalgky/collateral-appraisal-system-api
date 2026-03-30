using MassTransit;
using Shared.Messaging.Events;
using Workflow.Workflow.Events;

namespace Workflow.Tasks.EventHandlers;

public class ApprovalTasksAssignedEventHandler(
    IAssignmentRepository assignmentRepository,
    IPublishEndpoint publishEndpoint,
    ILogger<ApprovalTasksAssignedEventHandler> logger
) : INotificationHandler<ApprovalTasksAssignedEvent>
{
    public async Task Handle(ApprovalTasksAssignedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Handling ApprovalTasksAssignedEvent for CorrelationId {CorrelationId}, {MemberCount} members",
            notification.CorrelationId, notification.Members.Count);

        // Archive ALL existing PendingTasks for this CorrelationId (previous step or previous round)
        var existingTasks = await assignmentRepository.GetPendingTasksByCorrelationIdAsync(
            notification.CorrelationId, cancellationToken);

        foreach (var existingTask in existingTasks)
        {
            var completedTask = CompletedTask.CreateFromPendingTask(existingTask, "Reassigned", DateTime.UtcNow);
            await assignmentRepository.AddCompletedTaskAsync(completedTask, cancellationToken);
            await assignmentRepository.RemovePendingTaskAsync(existingTask, cancellationToken);
        }

        // Create one PendingTask per member
        foreach (var member in notification.Members)
        {
            var pendingTask = PendingTask.Create(
                notification.CorrelationId,
                member.TaskName,
                member.Username,
                "1", // Individual assignment
                notification.AssignedAt,
                notification.WorkflowInstanceId,
                notification.ActivityId,
                notification.DueAt,
                member.TaskDescription);

            await assignmentRepository.AddTaskAsync(pendingTask, cancellationToken);

            logger.LogInformation(
                "Created approval PendingTask {TaskId} for member {Username}, TaskName={TaskName}",
                pendingTask.Id, member.Username, member.TaskName);

            // Publish integration event per member for notifications
            await publishEndpoint.Publish(new TaskAssignedIntegrationEvent
            {
                CorrelationId = notification.CorrelationId,
                TaskName = member.TaskName,
                AssignedTo = member.Username,
                AssignedType = "1",
                StartedBy = notification.StartedBy,
                WorkflowInstanceName = notification.WorkflowInstanceName
            }, cancellationToken);
        }
    }
}
