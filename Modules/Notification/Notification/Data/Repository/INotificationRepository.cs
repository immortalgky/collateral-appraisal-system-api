using Notification.Notification.Models;

namespace Notification.Data.Repository;

public interface INotificationRepository
{
    Task<List<UserNotification>> GetUserNotificationsAsync(string userId, bool unreadOnly = false, int limit = 50);
    Task<UserNotification?> GetNotificationByIdAsync(Guid id);
    Task<UserNotification> AddNotificationAsync(UserNotification notification);
    Task UpdateNotificationAsync(UserNotification notification);
    Task MarkNotificationAsReadAsync(Guid id);
    Task MarkAllNotificationsAsReadAsync(string userId);
    Task<int> GetUnreadCountAsync(string userId);
    Task DeleteOldNotificationsAsync(DateTime cutoffDate);
}