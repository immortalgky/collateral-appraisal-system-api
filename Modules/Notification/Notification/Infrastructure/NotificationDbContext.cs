using Microsoft.EntityFrameworkCore;
using Notification.Domain.Email;
using Notification.Domain.Notifications.Models;
using Shared.Data.Outbox;
using Shared.Scheduling;

namespace Notification.Data;

public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options)
    {
    }

    public DbSet<UserNotification> UserNotifications { get; set; } = default!;
    public DbSet<EmailSendLog> EmailSendLogs { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("notification");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationDbContext).Assembly);

        modelBuilder.AddIntegrationEventInbox();

        // Admin-editable schedule for this module's recurring jobs (notification.JobSchedules)
        modelBuilder.AddJobSchedules();

        base.OnModelCreating(modelBuilder);
    }
}