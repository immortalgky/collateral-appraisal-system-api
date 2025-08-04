using Microsoft.EntityFrameworkCore;
using Notification.Notification.Models;

namespace Notification.Data.Repository;

public class NotificationRepository : INotificationRepository
{
    private readonly NotificationDbContext _context;

    public NotificationRepository(NotificationDbContext context)
    {
        _context = context;
    }

    public async Task<List<UserNotification>> GetUserNotificationsAsync(string userId, bool unreadOnly = false, int limit = 50)
    {
        var query = _context.UserNotifications
            .Where(n => n.UserId == userId);

        if (unreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<UserNotification?> GetNotificationByIdAsync(Guid id)
    {
        return await _context.UserNotifications
            .FirstOrDefaultAsync(n => n.Id == id);
    }

    public async Task<UserNotification> AddNotificationAsync(UserNotification notification)
    {
        _context.UserNotifications.Add(notification);
        await _context.SaveChangesAsync();
        return notification;
    }

    public async Task UpdateNotificationAsync(UserNotification notification)
    {
        _context.UserNotifications.Update(notification);
        await _context.SaveChangesAsync();
    }

    public async Task MarkNotificationAsReadAsync(Guid id)
    {
        var notification = await GetNotificationByIdAsync(id);
        if (notification != null)
        {
            notification.IsRead = true;
            await UpdateNotificationAsync(notification);
        }
    }

    public async Task MarkAllNotificationsAsReadAsync(string userId)
    {
        await _context.UserNotifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(n => n.SetProperty(x => x.IsRead, true));
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        return await _context.UserNotifications
            .CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task DeleteOldNotificationsAsync(DateTime cutoffDate)
    {
        await _context.UserNotifications
            .Where(n => n.CreatedAt < cutoffDate)
            .ExecuteDeleteAsync();
    }
}