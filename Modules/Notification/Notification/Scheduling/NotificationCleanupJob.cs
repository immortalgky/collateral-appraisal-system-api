using Dapper;
using Shared.Data;
using Shared.Time;

namespace Notification.Scheduling;

/// <summary>
/// Hangfire job that deletes <c>notification.UserNotifications</c> rows once read and 30 days old (or, read or
/// not, 90 days old) and <c>notification.EmailSendLogs</c> rows 90 days old. Both predicates lead with a bare
/// <c>CreatedAt &lt; @cutoff</c> so SQL can seek the <c>CreatedAt</c> index. Batched, like UserSessionCleanupJob,
/// so no long transaction holds the table.
/// </summary>
internal sealed class NotificationCleanupJob(
    ISqlConnectionFactory connectionFactory,
    ILogger<NotificationCleanupJob> logger,
    IDateTimeProvider dateTimeProvider)
{
    private const int ReadRetentionDays = 30;
    private const int RetentionDays = 90;
    // Stays under SQL Server's 5000-lock escalation threshold so a batch never escalates to a table X lock.
    private const int BatchSize = 4000;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.ApplicationNow;
        var readCutoff = now.AddDays(-ReadRetentionDays);
        var allCutoff = now.AddDays(-RetentionDays);
        var conn = connectionFactory.GetOpenConnection();
        var notifications = 0;
        var emailLogs = 0;
        int deleted;

        do
        {
            deleted = await conn.ExecuteAsync(
                "DELETE TOP(@BatchSize) FROM notification.UserNotifications " +
                "WHERE CreatedAt < @ReadCutoff AND (IsRead = 1 OR CreatedAt < @AllCutoff)",
                new { BatchSize, ReadCutoff = readCutoff, AllCutoff = allCutoff });
            notifications += deleted;
        } while (deleted == BatchSize && !cancellationToken.IsCancellationRequested);

        do
        {
            deleted = await conn.ExecuteAsync(
                "DELETE TOP(@BatchSize) FROM notification.EmailSendLogs WHERE CreatedAt < @AllCutoff",
                new { BatchSize, AllCutoff = allCutoff });
            emailLogs += deleted;
        } while (deleted == BatchSize && !cancellationToken.IsCancellationRequested);

        logger.LogInformation(
            "[NOTIFICATION-CLEANUP] Deleted {Notifications} notifications (read before {ReadCutoff}, any before {AllCutoff}) " +
            "and {EmailLogs} email send logs (before the same any-cutoff)",
            notifications, readCutoff, allCutoff, emailLogs);
    }
}
