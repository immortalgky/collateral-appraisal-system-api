using Notification.Notification.Dtos;
using Notification.Notification.Models;

namespace Notification.Notification.Services;

public interface INotificationService
{
    Task SendTaskAssignedNotificationAsync(TaskAssignedNotificationDto notification);
    Task SendTaskCompletedNotificationAsync(TaskCompletedNotificationDto notification);
    Task SendWorkflowProgressNotificationAsync(WorkflowProgressNotificationDto notification);
    Task SendNotificationToUserAsync(string userId, string title, string message, NotificationType type, string? actionUrl = null, Dictionary<string, object>? metadata = null);
    Task SendNotificationToGroupAsync(string groupName, string title, string message, NotificationType type, string? actionUrl = null, Dictionary<string, object>? metadata = null);
    Task<List<NotificationDto>> GetUserNotificationsAsync(string userId, bool unreadOnly = false);
    Task MarkNotificationAsReadAsync(Guid notificationId);
    Task MarkAllNotificationsAsReadAsync(string userId);
}