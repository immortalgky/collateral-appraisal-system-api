using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Time;

namespace Shared.Data.Outbox;

public class OutboxCleanupJob<TDbContext>(
    TDbContext dbContext,
    ILogger<OutboxCleanupJob<TDbContext>> logger,
    IDateTimeProvider dateTimeProvider)
    where TDbContext : DbContext
{
    private const int BatchSize = 1000;
    // Failed Messages PR only: read from OutboxDeliveryPolicy, which the outbox query handlers also read to
    // tell when a Processed sibling's history may already have been purged.
    private const int RetentionDays = OutboxDeliveryPolicy.ProcessedRetentionDays;

    // Failed Messages PR only: the reset-stuck-Processing UPDATE (NULL rule + 1-hour backstop) and the Failed
    // purge below changed here, so Sonar counts them as new code.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S2077:Formatting SQL queries is security-sensitive",
        Justification =
            "The only concatenated fragments are BatchSize (a compile-time const int) and the schema name, " +
            "read from the EF model (GetDefaultSchema) — never caller input. Statuses are literals and every " +
            "cutoff (including the two in the stuck-Processing reset: the NULL-ProcessingStartedAt OccurredAt " +
            "cutoff and the 1-hour ProcessingStartedAt backstop) is bound as a positional {n} parameter.")]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.ApplicationNow;
        var cutoff = now.AddDays(-RetentionDays);
        var failedCutoff = now.AddDays(-OutboxDeliveryPolicy.FailedRetentionDays);
        var schema = dbContext.Model.GetDefaultSchema() ?? "dbo";
        var totalDeleted = 0;

        logger.LogInformation("[OUTBOX-CLEANUP] Starting cleanup for {DbContext}, cutoff: {Cutoff}",
            typeof(TDbContext).Name, cutoff);

        // Reset rows left in Processing, two signatures only:
        //  (1) ProcessingStartedAt IS NULL: a node still on an OLD binary mid-rolling-deploy marks Processing
        //      without stamping it, so the only column this job can age such a row by is OccurredAt, against
        //      OutboxDeliveryPolicy's 1-hour threshold — long enough that no legitimate rolling deploy is
        //      mistaken for orphaned.
        //  (2) ProcessingStartedAt older than OutboxDeliveryPolicy.StuckProcessingBackstopThreshold (1 hour):
        //      the backstop for when the lease-holding delivery service's per-poll ResetOrphanedProcessingAsync
        //      keeps failing (its failures are swallowed) or never runs, so a stuck row must not wait
        //      forever. One hour is far beyond any possible batch (50 x 15s = 12.5 minutes, and mid-batch
        //      lease renewal keeps the lease), so a live batch is never reset.
        // A NON-NULL ProcessingStartedAt younger than that is deliberately NOT reset here: the delivery service's
        // own 5-minute reset owns it, and this daily job ignores the lease — a slow-but-alive batch (50
        // publishes x 7s is about 6 minutes) could have its in-flight rows reset at 02:00 and republished.
        var stuckCutoff = now - OutboxDeliveryPolicy.OrphanedProcessingThreshold; // stale inbox claims, below
        var nullStartedCutoff = now - OutboxDeliveryPolicy.NullProcessingStartedAtOrphanedThreshold;
        var backstopCutoff = now - OutboxDeliveryPolicy.StuckProcessingBackstopThreshold;
        var reset = await dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE [" + schema + "].[IntegrationEventOutbox] " +
            "SET Status = 'Pending', ProcessingStartedAt = NULL " +
            "WHERE Status = 'Processing' AND " +
            "((ProcessingStartedAt IS NULL AND OccurredAt < {0}) OR ProcessingStartedAt < {1})",
            new object[] { nullStartedCutoff, backstopCutoff }, cancellationToken);

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

        // Failed Messages PR only: delete dead-letter messages by their LAST activity — the latest claim, or
        // OccurredAt for a row that has none — so a row an operator resent that failed again is not purged
        // for being created long ago (design D7).
        do
        {
            deleted = await dbContext.Database.ExecuteSqlRawAsync(
                "DELETE TOP(" + BatchSize + ") FROM [" + schema + "].[IntegrationEventOutbox] " +
                "WHERE Status = 'Failed' AND COALESCE(ProcessingStartedAt, OccurredAt) < {0}",
                new object[] { failedCutoff }, cancellationToken);

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
