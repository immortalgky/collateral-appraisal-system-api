using Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Time;

namespace Integration.FailedMessages;

/// <summary>
/// Daily purge of <c>FailedMessages</c> rows an operator has already resolved (design D6). Only
/// Retried/Discarded rows age out, keyed on <c>ActionAt</c> (when the operator acted) rather than
/// <c>FaultedAt</c>. Pending/RetryRequested rows are never purged, and <c>FailedMessageAuditLogs</c> are
/// kept forever (design assumption 5). Also drops <c>BrokerSnapshots</c> not refreshed for
/// <see cref="BrokerSnapshotRetentionDays"/> — they are keyed by node and overwritten every round, so a
/// decommissioned or renamed node's row would otherwise show on the summary forever; a live node refreshes
/// its row every round and never gets near the threshold. Batches like
/// <see cref="Shared.Data.Outbox.OutboxCleanupJob{TDbContext}"/>.
/// </summary>
public class FailedMessageCleanupJob(
    IntegrationDbContext dbContext,
    ILogger<FailedMessageCleanupJob> logger,
    IDateTimeProvider dateTimeProvider)
{
    private const int BatchSize = 1000;
    private const int RetentionDays = 90;
    internal const int BrokerSnapshotRetentionDays = 7;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.ApplicationNow;
        var cutoff = now.AddDays(-RetentionDays);
        var snapshotCutoff = now.AddDays(-BrokerSnapshotRetentionDays);
        var schema = dbContext.Model.GetDefaultSchema() ?? "integration";

        logger.LogInformation("[FAILED-MSG-CLEANUP] Starting cleanup, cutoff: {Cutoff}, snapshot cutoff: {SnapshotCutoff}",
            cutoff, snapshotCutoff);

        var totalDeleted = await DeleteInBatchesAsync(
            schema, "FailedMessages", "Status IN ('Retried', 'Discarded') AND ActionAt < {0}", cutoff,
            cancellationToken);
        var snapshotsDeleted = await DeleteInBatchesAsync(
            schema, "BrokerSnapshots", "CollectedAt < {0}", snapshotCutoff, cancellationToken);

        logger.LogInformation(
            "[FAILED-MSG-CLEANUP] Completed, deleted {Count} rows and {Snapshots} broker snapshots",
            totalDeleted, snapshotsDeleted);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S2077:Formatting SQL queries is security-sensitive",
        Justification =
            "The only concatenated fragments are BatchSize (a compile-time const int), the schema name " +
            "(read from the EF model via GetDefaultSchema), and the table/predicate literals passed by " +
            "ExecuteAsync above — all hard-coded there, never caller input. The only value, the cutoff, " +
            "is bound as the {0} parameter.")]
    private async Task<int> DeleteInBatchesAsync(
        string schema, string table, string where, DateTime cutoff, CancellationToken cancellationToken)
    {
        var total = 0;
        int deleted;
        do
        {
            deleted = await dbContext.Database.ExecuteSqlRawAsync(
                "DELETE TOP(" + BatchSize + ") FROM [" + schema + "].[" + table + "] WHERE " + where,
                new object[] { cutoff }, cancellationToken);

            total += deleted;
        } while (deleted == BatchSize && !cancellationToken.IsCancellationRequested);

        return total;
    }
}
