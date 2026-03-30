using MassTransit;
using Notification.Data;
using Notification.Domain.Notifications.Dtos;
using Notification.Domain.Notifications.Models;
using Notification.Domain.Notifications.Services;
using Shared.Messaging.Filters;

namespace Notification.Domain.Notifications.EventHandlers;

public class TaskAssignedNotificationIntegrationEventHandler : IConsumer<TaskAssignedIntegrationEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<TaskAssignedNotificationIntegrationEventHandler> _logger;
    private readonly InboxGuard<NotificationDbContext> _inboxGuard;

    public TaskAssignedNotificationIntegrationEventHandler(
        INotificationService notificationService,
        ILogger<TaskAssignedNotificationIntegrationEventHandler> logger,
        InboxGuard<NotificationDbContext> inboxGuard)
    {
        _notificationService = notificationService;
        _logger = logger;
        _inboxGuard = inboxGuard;
    }

    public async Task Consume(ConsumeContext<TaskAssignedIntegrationEvent> context)
    {
        if (await _inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var taskAssigned = context.Message;

        _logger.LogInformation("Processing TaskAssigned notification for user {AssignedTo} and task {TaskName}",
            taskAssigned.AssignedTo, taskAssigned.TaskName);

        try
        {
            var appraisalNumber = taskAssigned.WorkflowInstanceName?
                .Replace("Appraisal-", "") ?? "N/A";

            var notification = new TaskAssignedNotificationDto(
                taskAssigned.CorrelationId,
                taskAssigned.TaskName,
                taskAssigned.AssignedTo,
                taskAssigned.AssignedType,
                appraisalNumber,
                taskAssigned.TaskName,
                DateTime.UtcNow
            );

            // Notify the new assignee
            await _notificationService.SendTaskAssignedNotificationAsync(notification);

            _logger.LogInformation("Successfully sent TaskAssigned notification for user {AssignedTo}",
                taskAssigned.AssignedTo);

            // Also notify the completer (person who completed the previous task)
            if (!string.IsNullOrEmpty(taskAssigned.CompletedBy) &&
                taskAssigned.CompletedBy != taskAssigned.AssignedTo)
            {
                var completerNotification = new TaskAssignedNotificationDto(
                    taskAssigned.CorrelationId,
                    taskAssigned.TaskName,
                    taskAssigned.AssignedTo,
                    taskAssigned.AssignedType,
                    appraisalNumber,
                    taskAssigned.TaskName,
                    DateTime.UtcNow,
                    NotifiedTo: taskAssigned.CompletedBy
                );

                await _notificationService.SendTaskAssignedToOtherNotificationAsync(completerNotification);

                _logger.LogInformation("Successfully sent TaskAssigned notification to completer {CompletedBy}",
                    taskAssigned.CompletedBy);
            }

            // Notify the requestor on first task assignment with appraisal number + assignee
            if (string.IsNullOrEmpty(taskAssigned.CompletedBy) &&
                !string.IsNullOrEmpty(taskAssigned.StartedBy))
            {
                await _notificationService.SendNotificationToUserAsync(
                    taskAssigned.StartedBy,
                    $"Request Submitted: Appraisal #{appraisalNumber}",
                    $"Your request has been submitted successfully. Appraisal #{appraisalNumber} has been assigned to {taskAssigned.AssignedTo} for {taskAssigned.TaskName}.",
                    NotificationType.WorkflowTransition,
                    metadata: new Dictionary<string, object>
                    {
                        { "correlationId", taskAssigned.CorrelationId },
                        { "appraisalNumber", appraisalNumber },
                        { "assignedTo", taskAssigned.AssignedTo },
                        { "taskName", taskAssigned.TaskName }
                    });

                _logger.LogInformation(
                    "Sent requestor notification to {StartedBy} for appraisal {AppraisalNumber}",
                    taskAssigned.StartedBy, appraisalNumber);
            }

            await _inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing TaskAssigned notification for user {AssignedTo} and task {TaskName}",
                taskAssigned.AssignedTo, taskAssigned.TaskName);
            throw;
        }
    }
}
