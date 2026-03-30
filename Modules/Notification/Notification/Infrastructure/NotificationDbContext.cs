using Microsoft.EntityFrameworkCore;
using Notification.Domain.Notifications.Models;
using Shared.Data.Outbox;

namespace Notification.Data;

public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options)
    {
    }

    public DbSet<UserNotification> UserNotifications { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("notification");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationDbContext).Assembly);

        modelBuilder.AddIntegrationEventInbox();

        base.OnModelCreating(modelBuilder);
    }
}