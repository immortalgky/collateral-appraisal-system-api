using MassTransit;
using Notification.Data;
using Notification.Domain.Notifications.Dtos;
using Notification.Domain.Notifications.Models;
using Notification.Domain.Notifications.Services;
using Shared.Messaging.Filters;

namespace Notification.Domain.Notifications.EventHandlers;

public class TaskAssignedNotificationIntegrationEventHandler : IConsumer<TaskAssignedIntegrationEvent>
{
    private static readonly HashSet<string> _rmNotifiableStages =
        ["ExtAppraisalAssignment", "IntAppraisalExecution", "PendingApproval"];

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
            var appraisalNumber = taskAssigned.AppraisalNumber ?? "N/A";

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

            // Notify RM (requestor) only at key milestone stages
            if (!string.IsNullOrEmpty(taskAssigned.StartedBy) &&
                taskAssigned.StartedBy != taskAssigned.AssignedTo &&
                taskAssigned.StartedBy != taskAssigned.CompletedBy &&
                _rmNotifiableStages.Contains(taskAssigned.TaskName))
            {
                await _notificationService.SendNotificationToUserAsync(
                    taskAssigned.StartedBy,
                    $"Request Progressed: {taskAssigned.TaskName}",
                    $"Your appraisal request (#{appraisalNumber}) has moved to the {taskAssigned.TaskName} stage.",
                    NotificationType.WorkflowTransition,
                    metadata: new Dictionary<string, object>
                    {
                        { "correlationId", taskAssigned.CorrelationId },
                        { "appraisalNumber", appraisalNumber },
                        { "stage", taskAssigned.TaskName }
                    });

                _logger.LogInformation("Successfully sent request-progressed notification to RM {StartedBy}",
                    taskAssigned.StartedBy);
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
