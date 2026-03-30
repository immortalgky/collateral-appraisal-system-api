using Notification.Domain.Notifications.Models;

namespace Notification.Data.Repository;

public interface INotificationRepository
{
    Task<List<UserNotification>> GetUserNotificationsAsync(string username, bool unreadOnly = false, int limit = 50);
    Task<UserNotification?> GetNotificationByIdAsync(Guid id);
    Task<UserNotification> AddNotificationAsync(UserNotification notification);
    Task UpdateNotificationAsync(UserNotification notification);
    Task MarkNotificationAsReadAsync(Guid id);
    Task MarkAllNotificationsAsReadAsync(string username);
    Task<int> GetUnreadCountAsync(string username);
    Task DeleteOldNotificationsAsync(DateTime cutoffDate);
}