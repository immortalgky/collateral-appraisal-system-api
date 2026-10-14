using Hangfire;
using Notification.Data;
using Shared.Data.Outbox;
using Shared.Scheduling;

namespace Notification.Scheduling;

/// <summary>
/// Recurring jobs owned by the Notification module. Seeded into and read from <c>notification.JobSchedules</c>
/// by <c>app.UseModuleRecurringJobs&lt;NotificationDbContext&gt;()</c>.
/// </summary>
public static class NotificationRecurringJobs
{
    public static readonly IReadOnlyList<RecurringJobDefinition> All = new[]
    {
        new RecurringJobDefinition("notification-cleanup", "30 3 * * *",
            "Delete read notifications after 30 days, all notifications and email send logs after 90 days (daily at 03:30).",
            (mgr, cron, opt) => mgr.AddOrUpdate<NotificationCleanupJob>(
                "notification-cleanup", j => j.ExecuteAsync(CancellationToken.None), cron, opt)),

        new RecurringJobDefinition("inbox-cleanup-notification", "0 2 * * *",
            "Purge processed/stale inbox messages for the Notification module (daily at 02:00).",
            (mgr, cron, opt) => mgr.AddOrUpdate<OutboxCleanupJob<NotificationDbContext>>(
                "inbox-cleanup-notification", j => j.ExecuteAsync(CancellationToken.None), cron, opt))
    };
}
