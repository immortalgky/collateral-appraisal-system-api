using Dapper;
using Microsoft.Extensions.Logging;
using Shared.Data;
using Shared.Time;

namespace Shared.Logging;

/// <summary>
/// Hangfire job that purges dbo.Logs rows older than 30 days.
/// Uses ISqlConnectionFactory + Dapper — no DbContext dependency.
/// Batched DELETEs avoid long-running transactions on large tables.
/// </summary>
public class LogsCleanupJob(
    ISqlConnectionFactory connectionFactory,
    ILogger<LogsCleanupJob> logger,
    IDateTimeProvider dateTimeProvider)
{
    private const int RetentionDays = 30;
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
                "DELETE TOP(@BatchSize) FROM dbo.Logs WHERE TimeStamp < @Cutoff",
                new { BatchSize, Cutoff = cutoff });

            total += deleted;
        } while (deleted == BatchSize && !cancellationToken.IsCancellationRequested);

        logger.LogInformation("[LOGS-CLEANUP] Deleted {Count} log rows older than {Cutoff}", total, cutoff);
    }
}
