using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Shared.Data.Outbox;

public class OutboxCleanupJob<TDbContext>(
    TDbContext dbContext,
    ILogger<OutboxCleanupJob<TDbContext>> logger)
    where TDbContext : DbContext
{
    private const int BatchSize = 1000;
    private const int RetentionDays = 7;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTime.Now.AddDays(-RetentionDays);
        var schema = dbContext.Model.GetDefaultSchema() ?? "dbo";
        var totalDeleted = 0;

        logger.LogInformation("[OUTBOX-CLEANUP] Starting cleanup for {DbContext}, cutoff: {Cutoff}",
            typeof(TDbContext).Name, cutoff);

        // Reset stuck Processing messages (instance crashed mid-batch) back to Pending
        var stuckCutoff = DateTime.Now.AddMinutes(-5);
        var reset = await dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE [" + schema + "].[IntegrationEventOutbox] " +
            "SET Status = 'Pending' " +
            "WHERE Status = 'Processing' AND OccurredAt < {0}",
            new object[] { stuckCutoff }, cancellationToken);

        if (reset > 0)
            logger.LogWarning("[OUTBOX-CLEANUP] Reset {Count} stuck Processing messages to Pending for {DbContext}",
                reset, typeof(TDbContext).Name);

        // Delete processed messages older than retention period
        int deleted;
        do
        {
            deleted = await dbContext.Database.ExecuteSqlRawAsync(
                "DELETE TOP(" + BatchSize + ") FROM [" + schema + "].[IntegrationEventOutbox] " +
                "WHERE Status = 'Processed' AND ProcessedAt < {0}",
                new object[] { cutoff }, cancellationToken);

            totalDeleted += deleted;
        } while (deleted == BatchSize && !cancellationToken.IsCancellationRequested);

        // Delete dead-letter messages older than retention period
        do
        {
            deleted = await dbContext.Database.ExecuteSqlRawAsync(
                "DELETE TOP(" + BatchSize + ") FROM [" + schema + "].[IntegrationEventOutbox] " +
                "WHERE Status = 'Failed' AND OccurredAt < {0}",
                new object[] { cutoff }, cancellationToken);

            totalDeleted += deleted;
        } while (deleted == BatchSize && !cancellationToken.IsCancellationRequested);

        // --- Inbox cleanup ---

        // Delete stale Processing inbox entries (crashed consumers)
        var staleInbox = await dbContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM [" + schema + "].[InboxMessage] " +
            "WHERE Status = 'Processing' AND StartedAt < {0}",
            new object[] { stuckCutoff }, cancellationToken);

        if (staleInbox > 0)
            logger.LogWarning("[OUTBOX-CLEANUP] Deleted {Count} stale Processing inbox entries for {DbContext}",
                staleInbox, typeof(TDbContext).Name);

        // Delete processed inbox entries older than retention period
        do
        {
            deleted = await dbContext.Database.ExecuteSqlRawAsync(
                "DELETE TOP(" + BatchSize + ") FROM [" + schema + "].[InboxMessage] " +
                "WHERE Status = 'Processed' AND ProcessedAt < {0}",
                new object[] { cutoff }, cancellationToken);

            totalDeleted += deleted;
        } while (deleted == BatchSize && !cancellationToken.IsCancellationRequested);

        logger.LogInformation("[OUTBOX-CLEANUP] Completed for {DbContext}, deleted {Count} outbox+inbox entries",
            typeof(TDbContext).Name, totalDeleted);
    }
}
