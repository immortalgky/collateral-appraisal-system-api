using MassTransit;
using Shared.Messaging.Events;
using Workflow.Workflow.Events;

namespace Workflow.Tasks.EventHandlers;

/// <summary>
/// Creates one PendingTask per company when a FanOutTaskActivity fires.
/// Each row carries the company's assigned-to principal AND the AssigneeCompanyId for filtering.
/// Archives any pre-existing tasks scoped to this workflow instance first.
/// </summary>
public class FanOutTasksAssignedEventHandler(
    IAssignmentRepository assignmentRepository,
    IPublishEndpoint publishEndpoint,
    IDateTimeProvider dateTimeProvider,
    ILogger<FanOutTasksAssignedEventHandler> logger
) : INotificationHandler<FanOutTasksAssignedEvent>
{
    public async Task Handle(FanOutTasksAssignedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Handling FanOutTasksAssignedEvent for CorrelationId {CorrelationId}, {CompanyCount} companies",
            notification.CorrelationId, notification.Companies.Count);

        // Archive ALL existing PendingTasks from the previous step of this workflow instance
        var existingTasks = await assignmentRepository.GetPendingTasksByWorkflowInstanceIdAsync(
            notification.WorkflowInstanceId, cancellationToken);

        foreach (var existing in existingTasks)
        {
            var archived = CompletedTask.CreateFromPendingTask(
                existing, "Reassigned", dateTimeProvider.ApplicationNow);
            await assignmentRepository.AddCompletedTaskAsync(archived, cancellationToken);
            await assignmentRepository.RemovePendingTaskAsync(existing, cancellationToken);
        }

        // Create one PendingTask per company
        foreach (var assignment in notification.Companies)
        {
            var pendingTask = PendingTask.Create(
                notification.CorrelationId,
                assignment.TaskName,
                assignment.AssignedTo,
                assignedType: "2",           // Pool/company-scoped
                notification.AssignedAt,
                notification.WorkflowInstanceId,
                notification.ActivityId,
                notification.DueAt,
                assignment.TaskDescription,
                notification.Movement,
                assigneeCompanyId: assignment.CompanyId);

            await assignmentRepository.AddTaskAsync(pendingTask, cancellationToken);

            logger.LogInformation(
                "Created fan-out PendingTask {TaskId} for Company {CompanyId} at activity {ActivityId}",
                pendingTask.Id, assignment.CompanyId, notification.ActivityId);

            await publishEndpoint.Publish(new TaskAssignedIntegrationEvent
            {
                CorrelationId = notification.CorrelationId,
                TaskName = assignment.TaskName,
                AssignedTo = assignment.AssignedTo,
                AssignedType = "2",
                StartedBy = notification.StartedBy,
                WorkflowInstanceName = notification.WorkflowInstanceName,
                AppraisalNumber = notification.AppraisalNumber
            }, cancellationToken);
        }
    }
}
