using Notification.Domain.Notifications.Dtos;
using Notification.Domain.Notifications.Models;

namespace Notification.Domain.Notifications.Services;

public interface INotificationService
{
    Task SendTaskAssignedNotificationAsync(TaskAssignedNotificationDto notification);
    Task SendTaskAssignedToOtherNotificationAsync(TaskAssignedNotificationDto notification);
    Task SendTaskCompletedNotificationAsync(TaskCompletedNotificationDto notification);
    Task SendWorkflowProgressNotificationAsync(WorkflowProgressNotificationDto notification);
    Task SendNotificationToUserAsync(string username, string title, string message, NotificationType type, string? actionUrl = null, Dictionary<string, object>? metadata = null);
    Task SendNotificationToGroupAsync(string groupName, string title, string message, NotificationType type, string? actionUrl = null, Dictionary<string, object>? metadata = null);
    Task<List<NotificationDto>> GetUserNotificationsAsync(string username, bool unreadOnly = false);
    Task MarkNotificationAsReadAsync(Guid notificationId);
    Task MarkAllNotificationsAsReadAsync(string username);
}