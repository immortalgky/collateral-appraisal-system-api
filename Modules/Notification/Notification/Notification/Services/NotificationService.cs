using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Notification.Data.Repository;
using Notification.Notification.Dtos;
using Notification.Notification.Hubs;
using Notification.Notification.Models;
using Shared.Time;

namespace Notification.Notification.Services;

public class NotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<NotificationService> _logger;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly INotificationRepository _notificationRepository;

    public NotificationService(
        IHubContext<NotificationHub> hubContext,
        ILogger<NotificationService> logger,
        IDateTimeProvider dateTimeProvider,
        INotificationRepository notificationRepository)
    {
        _hubContext = hubContext;
        _logger = logger;
        _dateTimeProvider = dateTimeProvider;
        _notificationRepository = notificationRepository;
    }

    public async Task SendTaskAssignedNotificationAsync(TaskAssignedNotificationDto notification)
    {
        var title = $"New Task Assigned: {notification.TaskName}";
        var message = $"You have been assigned a new task for Request #{notification.RequestId} in the {notification.CurrentState} stage.";

        await SendNotificationToUserAsync(
            notification.AssignedTo,
            title,
            message,
            NotificationType.TaskAssigned,
            $"/requests/{notification.RequestId}/tasks",
            new Dictionary<string, object>
            {
                { "correlationId", notification.CorrelationId },
                { "taskName", notification.TaskName },
                { "requestId", notification.RequestId },
                { "currentState", notification.CurrentState },
                { "assignedType", notification.AssignedType }
            });

        _logger.LogInformation("Sent task assigned notification to user {UserId} for task {TaskName}", 
            notification.AssignedTo, notification.TaskName);
    }

    public async Task SendTaskCompletedNotificationAsync(TaskCompletedNotificationDto notification)
    {
        var title = $"Task Completed: {notification.TaskName}";
        var actionText = notification.ActionTaken == "P" ? "approved" : "returned for revision";
        var message = $"Task {notification.TaskName} has been {actionText} by {notification.CompletedBy}. Request #{notification.RequestId} moved from {notification.PreviousState} to {notification.NextState}.";

        // Notify relevant users about the completion
        await SendNotificationToGroupAsync(
            $"Request_{notification.RequestId}",
            title,
            message,
            NotificationType.TaskCompleted,
            $"/requests/{notification.RequestId}",
            new Dictionary<string, object>
            {
                { "correlationId", notification.CorrelationId },
                { "taskName", notification.TaskName },
                { "requestId", notification.RequestId },
                { "completedBy", notification.CompletedBy },
                { "actionTaken", notification.ActionTaken },
                { "previousState", notification.PreviousState },
                { "nextState", notification.NextState }
            });

        _logger.LogInformation("Sent task completed notification for task {TaskName} completed by {CompletedBy}", 
            notification.TaskName, notification.CompletedBy);
    }

    public async Task SendWorkflowProgressNotificationAsync(WorkflowProgressNotificationDto notification)
    {
        var title = $"Workflow Update: Request #{notification.RequestId}";
        var message = $"Workflow is now in {notification.CurrentState} stage.";
        
        if (!string.IsNullOrEmpty(notification.NextAssignee))
        {
            message += $" Next assigned to: {notification.NextAssignee}";
        }

        await SendNotificationToGroupAsync(
            $"Request_{notification.RequestId}",
            title,
            message,
            NotificationType.WorkflowTransition,
            $"/requests/{notification.RequestId}/workflow",
            new Dictionary<string, object>
            {
                { "correlationId", notification.CorrelationId },
                { "requestId", notification.RequestId },
                { "currentState", notification.CurrentState },
                { "nextAssignee", notification.NextAssignee ?? "" },
                { "nextAssigneeType", notification.NextAssigneeType ?? "" },
                { "workflowSteps", notification.WorkflowSteps }
            });

        _logger.LogInformation("Sent workflow progress notification for request {RequestId} in state {CurrentState}", 
            notification.RequestId, notification.CurrentState);
    }

    public async Task SendNotificationToUserAsync(string userId, string title, string message, NotificationType type, string? actionUrl = null, Dictionary<string, object>? metadata = null)
    {
        var notification = new UserNotification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            CreatedAt = _dateTimeProvider.UtcNow,
            IsRead = false,
            ActionUrl = actionUrl,
            Metadata = metadata
        };

        // Store in database
        await _notificationRepository.AddNotificationAsync(notification);

        // Send real-time notification via SignalR
        await _hubContext.Clients.Group($"User_{userId}")
            .SendAsync("ReceiveNotification", new NotificationDto(
                notification.Id,
                notification.Title,
                notification.Message,
                notification.Type,
                notification.CreatedAt,
                notification.IsRead,
                notification.ActionUrl,
                notification.Metadata));

        _logger.LogInformation("Sent notification to user {UserId}: {Title}", userId, title);
    }

    public async Task SendNotificationToGroupAsync(string groupName, string title, string message, NotificationType type, string? actionUrl = null, Dictionary<string, object>? metadata = null)
    {
        await _hubContext.Clients.Group(groupName)
            .SendAsync("ReceiveGroupNotification", new
            {
                Title = title,
                Message = message,
                Type = type.ToString(),
                CreatedAt = _dateTimeProvider.UtcNow,
                ActionUrl = actionUrl,
                Metadata = metadata
            });

        _logger.LogInformation("Sent group notification to {GroupName}: {Title}", groupName, title);
    }

    public async Task<List<NotificationDto>> GetUserNotificationsAsync(string userId, bool unreadOnly = false)
    {
        var notifications = await _notificationRepository.GetUserNotificationsAsync(userId, unreadOnly);
        
        return notifications.Select(n => new NotificationDto(
            n.Id,
            n.Title,
            n.Message,
            n.Type,
            n.CreatedAt,
            n.IsRead,
            n.ActionUrl,
            n.Metadata))
            .ToList();
    }

    public async Task MarkNotificationAsReadAsync(Guid notificationId)
    {
        await _notificationRepository.MarkNotificationAsReadAsync(notificationId);
        _logger.LogInformation("Marked notification {NotificationId} as read", notificationId);
    }

    public async Task MarkAllNotificationsAsReadAsync(string userId)
    {
        await _notificationRepository.MarkAllNotificationsAsReadAsync(userId);
        _logger.LogInformation("Marked all notifications as read for user {UserId}", userId);
    }
}