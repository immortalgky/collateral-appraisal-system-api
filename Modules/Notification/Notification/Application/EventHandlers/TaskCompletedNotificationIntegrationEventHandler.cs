using MassTransit;
using Notification.Data;
using Notification.Domain.Notifications.Dtos;
using Notification.Domain.Notifications.Services;
using Shared.Messaging.Filters;

namespace Notification.Domain.Notifications.EventHandlers;

public class TaskCompletedNotificationIntegrationEventHandler : IConsumer<TaskCompletedIntegrationEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<TaskCompletedNotificationIntegrationEventHandler> _logger;
    private readonly InboxGuard<NotificationDbContext> _inboxGuard;

    public TaskCompletedNotificationIntegrationEventHandler(
        INotificationService notificationService,
        ILogger<TaskCompletedNotificationIntegrationEventHandler> logger,
        InboxGuard<NotificationDbContext> inboxGuard)
    {
        _notificationService = notificationService;
        _logger = logger;
        _inboxGuard = inboxGuard;
    }

    public async Task Consume(ConsumeContext<TaskCompletedIntegrationEvent> context)
    {
        if (await _inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var taskCompleted = context.Message;

        _logger.LogInformation("Processing TaskCompleted notification for task {TaskName} with action {ActionTaken}",
            taskCompleted.TaskName, taskCompleted.ActionTaken);

        try
        {
            var completedBy = taskCompleted.CompletedBy ?? "System";
            var appraisalNumber = taskCompleted.WorkflowInstanceName?
                .Replace("Appraisal-", "") ?? "N/A";

            var notification = new TaskCompletedNotificationDto(
                taskCompleted.CorrelationId,
                taskCompleted.TaskName,
                completedBy,
                taskCompleted.ActionTaken,
                appraisalNumber,
                GetPreviousState(taskCompleted.TaskName),
                GetNextState(taskCompleted.TaskName, taskCompleted.ActionTaken),
                DateTime.UtcNow
            );

            await _notificationService.SendTaskCompletedNotificationAsync(notification);

            _logger.LogInformation("Successfully sent TaskCompleted notification for task {TaskName}",
                taskCompleted.TaskName);

            await _inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing TaskCompleted notification for task {TaskName}",
                taskCompleted.TaskName);
            throw;
        }
    }

    private static string GetPreviousState(string taskName)
    {
        return taskName switch
        {
            "Admin" => "AwaitingAssignment",
            "ExtAppraisalStaff" => "Admin",
            "ExtAppraisalChecker" => "ExtAppraisalStaff",
            "ExtAppraisalVerifier" => "ExtAppraisalChecker",
            "IntAppraisalStaff" => "ExtAppraisalVerifier",
            "IntAppraisalChecker" => "IntAppraisalStaff",
            "IntAppraisalVerifier" => "IntAppraisalChecker",
            "PendingApproval" => "IntAppraisalVerifier",
            _ => "Unknown"
        };
    }

    private static string GetNextState(string taskName, string actionTaken)
    {
        if (actionTaken == "R")
            return taskName switch
            {
                "Admin" => "RequestMaker",
                "ExtAppraisalStaff" => "Admin",
                "ExtAppraisalChecker" => "ExtAppraisalStaff",
                "ExtAppraisalVerifier" => "ExtAppraisalChecker",
                "IntAppraisalStaff" => "ExtAppraisalStaff",
                "IntAppraisalChecker" => "IntAppraisalStaff",
                "IntAppraisalVerifier" => "IntAppraisalChecker",
                "PendingApproval" => "IntAppraisalStaff",
                _ => "Unknown"
            };
        else
            return taskName switch
            {
                "Admin" => "ExtAppraisalStaff",
                "ExtAppraisalStaff" => "ExtAppraisalChecker",
                "ExtAppraisalChecker" => "ExtAppraisalVerifier",
                "ExtAppraisalVerifier" => "IntAppraisalStaff",
                "IntAppraisalStaff" => "IntAppraisalChecker",
                "IntAppraisalChecker" => "IntAppraisalVerifier",
                "IntAppraisalVerifier" => "PendingApproval",
                "PendingApproval" => "Completed",
                _ => "Unknown"
            };
    }
}
