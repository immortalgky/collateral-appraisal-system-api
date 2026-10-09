using Integration.Domain.FailedMessages;
using Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Time;

namespace Integration.FailedMessages;

/// <summary>
/// Daily purge of <c>FailedMessages</c> rows an operator has already resolved (design D6). Only
/// Retried/Discarded rows age out, keyed on <c>ActionAt</c> (when the operator acted) rather than
/// <c>FaultedAt</c>. Pending/RetryRequested rows are never purged, and <c>FailedMessageAuditLogs</c> are
/// kept forever (design assumption 5). Also drops <c>BrokerSnapshots</c> not refreshed for
/// <see cref="BrokerSnapshotRetentionDays"/> — they are keyed by node and overwritten every round, so a
/// decommissioned or renamed node's row would otherwise show on the summary forever; a live node refreshes
/// its row every round and never gets near the threshold. Also drops <c>Kind = Skipped</c> rows still
/// <c>Pending</c> after <see cref="FailedMessagesOptions.SkippedPendingRetentionDays"/> (default 30): no consumer
/// handles that message type, so Retry cannot help and a broken binding would grow the table without bound.
/// Error rows are never touched by that rule, whatever their status. Batches like
/// <see cref="Shared.Data.Outbox.OutboxCleanupJob{TDbContext}"/>.
/// </summary>
public class FailedMessageCleanupJob(
    IntegrationDbContext dbContext,
    ILogger<FailedMessageCleanupJob> logger,
    IDateTimeProvider dateTimeProvider,
    IOptions<FailedMessagesOptions> options)
{
    private readonly int _skippedPendingRetentionDays = options.Value.SkippedPendingRetentionDays;

    private const int BatchSize = 1000;

    /// <summary>Shared by the traceability SELECT and the DELETE so the logged rows are exactly the purged ones.
    /// Keyed on the later of the last admin action and collection: ActionAt is null for a row nobody touched, and
    /// RevertRetry (retry whose original queue is gone) sets it to now while putting the row back to Pending, so that
    /// row gets a fresh grace window instead of being purged the next night. Not FaultedAt: for a Skipped row that
    /// is the AMQP/envelope send time, possibly long before we collected it.</summary>
    private const string SkippedPendingPredicate =
        $"Kind = '{FailedMessageKind.Skipped}' AND Status = '{FailedMessageStatus.Pending}' " +
        "AND COALESCE(ActionAt, CollectedAt) < {0}";

    private const int RetentionDays = 90;
    internal const int BrokerSnapshotRetentionDays = 7;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.ApplicationNow;
        var cutoff = now.AddDays(-RetentionDays);
        var snapshotCutoff = now.AddDays(-BrokerSnapshotRetentionDays);
        var skippedPendingCutoff = now.AddDays(-_skippedPendingRetentionDays);
        var schema = dbContext.Model.GetDefaultSchema() ?? "integration";

        logger.LogInformation("[FAILED-MSG-CLEANUP] Starting cleanup, cutoff: {Cutoff}, snapshot cutoff: {SnapshotCutoff}, skipped-pending cutoff: {SkippedPendingCutoff}",
            cutoff, snapshotCutoff, skippedPendingCutoff);

        var totalDeleted = await DeleteInBatchesAsync(
            schema, "FailedMessages",
            $"Status IN ('{FailedMessageStatus.Retried}', '{FailedMessageStatus.Discarded}') AND ActionAt < {{0}}",
            cutoff, cancellationToken);
        await LogSkippedPendingAboutToBePurgedAsync(schema, skippedPendingCutoff, cancellationToken);
        var skippedPendingDeleted = await DeleteInBatchesAsync(
            schema, "FailedMessages", SkippedPendingPredicate, skippedPendingCutoff, cancellationToken);
        var snapshotsDeleted = await DeleteInBatchesAsync(
            schema, "BrokerSnapshots", "CollectedAt < {0}", snapshotCutoff, cancellationToken);

        logger.LogInformation(
            "[FAILED-MSG-CLEANUP] Completed, deleted {Count} resolved rows, {SkippedPending} pending skipped rows and {Snapshots} broker snapshots",
            totalDeleted, skippedPendingDeleted, snapshotsDeleted);
    }

    /// <summary>One grouped SELECT (no per-row work) so an operator can see what the nightly purge is about to remove.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S2077:Formatting SQL queries is security-sensitive",
        Justification =
            "The only concatenated fragments are the schema name (read from the EF model via GetDefaultSchema) " +
            "and SkippedPendingPredicate, a compile-time const built from string literals and the const " +
            "FailedMessageKind/FailedMessageStatus names — never caller input or configuration. The only value, " +
            "the cutoff, is bound as the {0} parameter.")]
    private async Task LogSkippedPendingAboutToBePurgedAsync(
        string schema, DateTime cutoff, CancellationToken cancellationToken)
    {
        var groups = await dbContext.Database.SqlQueryRaw<SkippedPendingGroup>(
                "SELECT SourceQueue, MessageType, COUNT(*) AS [Count], MIN(CollectedAt) AS OldestCollectedAt, " +
                "MAX(CollectedAt) AS NewestCollectedAt FROM [" + schema + "].[FailedMessages] WHERE " +
                SkippedPendingPredicate + " GROUP BY SourceQueue, MessageType",
                cutoff)
            .ToListAsync(cancellationToken);

        foreach (var g in groups)
            logger.LogInformation(
                "[FAILED-MSG-CLEANUP] Purging {Count} pending skipped rows from {SourceQueue} ({MessageType}) older than {Days} days (collected {OldestCollectedAt} to {NewestCollectedAt})",
                g.Count, g.SourceQueue, g.MessageType, _skippedPendingRetentionDays, g.OldestCollectedAt,
                g.NewestCollectedAt);
    }

    private sealed record SkippedPendingGroup(
        string SourceQueue, string MessageType, int Count, DateTime OldestCollectedAt, DateTime NewestCollectedAt);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S2077:Formatting SQL queries is security-sensitive",
        Justification =
            "The only concatenated fragments are BatchSize (a compile-time const int), the schema name " +
            "(read from the EF model via GetDefaultSchema), and the table/predicate strings passed by " +
            "ExecuteAsync above — string literals, SkippedPendingPredicate (compile-time const built from literals " +
            "and the const FailedMessageKind/FailedMessageStatus names), never caller input or configuration. The only values, " +
            "the cutoffs, are bound as the {0} parameter; SkippedPendingRetentionDays only moves the cutoff " +
            "DateTime and is never concatenated into the SQL.")]
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
