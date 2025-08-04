using Microsoft.EntityFrameworkCore;
using Notification.Notification.Models;
using Shared.Data.Seed;

namespace Notification.Data.Seed;

public class NotificationDataSeed : IDataSeeder<NotificationDbContext>
{
    private readonly NotificationDbContext _context;

    public NotificationDataSeed(NotificationDbContext context)
    {
        _context = context;
    }

    public async Task SeedAllAsync()
    {
        await _context.Database.MigrateAsync();

        if (await _context.UserNotifications.AnyAsync())
        {
            return; // Already seeded
        }

        var sampleNotifications = new List<UserNotification>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = "admin",
                Title = "Welcome to Notification System",
                Message = "Real-time notifications are now active for your collateral appraisal workflow.",
                Type = NotificationType.SystemNotification,
                CreatedAt = DateTime.UtcNow.AddHours(-1),
                IsRead = false,
                ActionUrl = "/dashboard",
                Metadata = new Dictionary<string, object>
                {
                    { "category", "system" },
                    { "priority", "info" }
                }
            },
            new()
            {
                Id = Guid.NewGuid(),
                UserId = "testuser",
                Title = "New Task Assigned: Admin Review",
                Message = "You have been assigned a new task for Request #1 in the Admin stage.",
                Type = NotificationType.TaskAssigned,
                CreatedAt = DateTime.UtcNow.AddMinutes(-30),
                IsRead = false,
                ActionUrl = "/requests/1/tasks",
                Metadata = new Dictionary<string, object>
                {
                    { "correlationId", Guid.NewGuid().ToString() },
                    { "taskName", "Admin" },
                    { "requestId", 1 },
                    { "currentState", "Admin" },
                    { "assignedType", "U" }
                }
            },
            new()
            {
                Id = Guid.NewGuid(),
                UserId = "testuser",
                Title = "Task Completed: Appraisal Review",
                Message = "Task AppraisalStaff has been approved by appraiser1. Request #2 moved from AppraisalStaff to AppraisalChecker.",
                Type = NotificationType.TaskCompleted,
                CreatedAt = DateTime.UtcNow.AddMinutes(-15),
                IsRead = true,
                ActionUrl = "/requests/2",
                Metadata = new Dictionary<string, object>
                {
                    { "correlationId", Guid.NewGuid().ToString() },
                    { "taskName", "AppraisalStaff" },
                    { "requestId", 2 },
                    { "completedBy", "appraiser1" },
                    { "actionTaken", "P" },
                    { "previousState", "AppraisalStaff" },
                    { "nextState", "AppraisalChecker" }
                }
            }
        };

        _context.UserNotifications.AddRange(sampleNotifications);
        await _context.SaveChangesAsync();
    }
}