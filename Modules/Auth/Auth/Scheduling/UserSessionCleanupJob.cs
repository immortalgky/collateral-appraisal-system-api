using Dapper;
using Shared.Time;

namespace Auth.Scheduling;

/// <summary>
/// Hangfire job that deletes <c>auth.UserSessions</c> rows once they are 90 days past their end (or, for
/// a row that was never explicitly ended, past their last activity). A session ends no earlier than its last
/// activity, so the <c>LastActivityAt</c> bound is implied and lets SQL seek <c>IX_UserSessions_LastActivityAt</c>. Batched, like LogsCleanupJob, so
/// no long transaction holds the table.
/// </summary>
internal sealed class UserSessionCleanupJob(
    ISqlConnectionFactory connectionFactory,
    ILogger<UserSessionCleanupJob> logger,
    IDateTimeProvider dateTimeProvider)
{
    private const int RetentionDays = 90;
    private const int BatchSize = 5000;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var cutoff = dateTimeProvider.ApplicationNow.AddDays(-RetentionDays);
        var conn = connectionFactory.GetOpenConnection();
        var total = 0;
        int deleted;

        do
        {
            deleted = await conn.ExecuteAsync(
                "DELETE TOP(@BatchSize) FROM auth.UserSessions " +
                "WHERE LastActivityAt < @Cutoff AND COALESCE(EndedAt, LastActivityAt) < @Cutoff",
                new { BatchSize, Cutoff = cutoff });
            total += deleted;
        } while (deleted == BatchSize && !cancellationToken.IsCancellationRequested);

        logger.LogInformation("[SESSION-CLEANUP] Deleted {Count} session rows older than {Cutoff}", total, cutoff);
    }
}
