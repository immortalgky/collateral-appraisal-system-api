using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Data.Outbox;
using Shared.Time;

namespace Shared.Messaging.Filters;

public class InboxGuard<TDbContext>(
    TDbContext dbContext,
    ILogger<InboxGuard<TDbContext>> logger,
    IDateTimeProvider dateTimeProvider)
    where TDbContext : DbContext
{
    private const int StaleThresholdMinutes = 5;

    /// <summary>
    /// Returns true if the message should be SKIPPED (already processed or being processed).
    /// Returns false if the message was claimed and should be processed.
    /// </summary>
    public async Task<bool> TryClaimAsync(Guid? messageId, string consumerType, CancellationToken ct)
    {
        if (messageId is null)
            return false; // No message ID — can't deduplicate, process anyway

        var id = messageId.Value;

        // Try to INSERT as Processing (claim the message)
        try
        {
            var inbox = InboxMessage.Create(id, consumerType, dateTimeProvider.ApplicationNow);
            dbContext.Set<InboxMessage>().Add(inbox);
            await dbContext.SaveChangesAsync(ct);
            return false; // Claimed successfully — proceed with processing
        }
        catch (DbUpdateException)
        {
            // PK violation — row already exists. Check its state.
            dbContext.ChangeTracker.Clear();
        }

        var existing = await dbContext.Set<InboxMessage>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.MessageId == id && x.ConsumerType == consumerType, ct);

        if (existing is null)
            return false; // Row disappeared (concurrent delete) — safe to retry

        if (existing.Status == InboxMessageStatus.Processed)
        {
            logger.LogInformation("[INBOX] Message {MessageId} already processed by {Consumer}, skipping",
                id, consumerType);
            return true; // Already processed — skip
        }

        // Status is Processing — check if stale
        if (existing.StartedAt < dateTimeProvider.ApplicationNow.AddMinutes(-StaleThresholdMinutes))
        {
            // Stale Processing — another instance crashed. Delete and re-claim.
            logger.LogWarning("[INBOX] Stale Processing message {MessageId} by {Consumer} (started {StartedAt}), reclaiming",
                id, consumerType, existing.StartedAt);

            var schema = dbContext.Model.GetDefaultSchema() ?? "dbo";
            await dbContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM [" + schema + "].[InboxMessage] " +
                "WHERE MessageId = {0} AND ConsumerType = {1} AND Status = 'Processing'",
                new object[] { id, consumerType }, ct);

            // Re-INSERT as Processing
            try
            {
                var inbox = InboxMessage.Create(id, consumerType, dateTimeProvider.ApplicationNow);
                dbContext.Set<InboxMessage>().Add(inbox);
                await dbContext.SaveChangesAsync(ct);
                return false; // Reclaimed — proceed
            }
            catch (DbUpdateException)
            {
                // Another instance reclaimed it first
                return true;
            }
        }

        // Processing by another consumer recently — skip
        logger.LogInformation("[INBOX] Message {MessageId} being processed by another instance for {Consumer}, skipping",
            id, consumerType);
        return true;
    }

    /// <summary>
    /// Drop a claim taken by <see cref="TryClaimAsync"/> when the consumer could not finish its work and
    /// wants the bus retry to have another go.
    ///
    /// Without this, a claim followed by a throw is a silent dead end: the row stays 'Processing', and
    /// every bus retry inside the 5-minute stale window sees it and skips, so the consumer returns
    /// normally and the message is acked with the work never done and nothing dead-lettered.
    /// Only removes rows still in 'Processing' — a completed claim is left alone.
    /// </summary>
    /// <param name="claimedBefore">
    /// A timestamp taken by the caller immediately after <see cref="TryClaimAsync"/> let it proceed. Rows
    /// started after it belong to somebody else and are left alone.
    ///
    /// This matters because (MessageId, ConsumerType) alone does not identify *whose* claim a row is. If
    /// this caller stalls past the 5-minute stale window, another instance's retry legitimately reclaims
    /// the message — deleting that fresh row would strand the message with no inbox record at all, so a
    /// later redelivery would run the work a second time.
    ///
    /// It narrows the window rather than closing it: <see cref="TryClaimAsync"/> also returns "proceed"
    /// when a PK collision is followed by the row having vanished, and in that case the caller holds no
    /// row of its own, so a concurrent claim taken microseconds earlier still matches. Closing that last
    /// gap needs a per-claim token on the row, which is a schema change to a table every consumer shares.
    /// </param>
    public async Task ReleaseClaimAsync(
        Guid? messageId, string consumerType, DateTime claimedBefore, CancellationToken ct)
    {
        if (messageId is null) return;

        // Runs on a FRESH connection, not dbContext. Callers reach this from a catch block, and the most
        // likely reason they are there is a dropped connection or a SQL timeout — the same context this
        // DELETE would otherwise use. A release that fails whenever the thing it compensates for fails is
        // no release at all: the row stays 'Processing', every bus retry inside the stale window is told to
        // skip, and the first of them acks the message with the work never done.
        //
        // No tolerance on the timestamp comparison, deliberately. EF Core maps a DateTime argument to
        // SqlDbType.DateTime2 (verified against SQL Server, not assumed), the same type as
        // InboxMessage.StartedAt, so the comparison is exact — slack would only widen the window this
        // parameter exists to narrow.
        var schema = dbContext.Model.GetDefaultSchema() ?? "dbo";

        // A sibling of the context's connection rather than an injected factory: taking a new constructor
        // dependency on ISqlConnectionFactory would ripple into every consumer's unit tests for no extra
        // capability. Cloning by runtime type keeps this provider-agnostic.
        //
        // The string MUST come from Database.GetConnectionString() (what the context was configured with),
        // not from GetDbConnection().ConnectionString. SqlConnection strips Password= from the latter once
        // it has been opened — Persist Security Info is false by default — and by the time anyone calls this
        // the context has already opened its connection inside TryClaimAsync. Cloning the live string
        // produces "Login failed for user" on every single call under SQL auth. Verified both ways against
        // the dev server; do not "simplify" this back.
        var contextConnection = dbContext.Database.GetDbConnection();

        await using var connection = (DbConnection)Activator.CreateInstance(contextConnection.GetType())!;
        connection.ConnectionString = dbContext.Database.GetConnectionString();
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText =
            "DELETE FROM [" + schema + "].[InboxMessage] " +
            "WHERE MessageId = @MessageId AND ConsumerType = @ConsumerType " +
            "AND Status = 'Processing' AND StartedAt <= @ClaimedBefore";

        AddParameter(command, "@MessageId", messageId.Value);
        AddParameter(command, "@ConsumerType", consumerType);

        // DbType is set explicitly ONLY because this is raw ADO. EF's type mapping used to hand SQL Server a
        // datetime2 for us; a SqlParameter left to infer picks SqlDbType.DateTime instead — 3.33 ms
        // granularity, rounded to nearest (verified, not assumed). StartedAt is datetime2, so an inferred
        // parameter can round BELOW the timestamp it was taken after, the predicate goes false, the DELETE
        // matches nothing, and the claim is silently never released.
        AddParameter(command, "@ClaimedBefore", claimedBefore, DbType.DateTime2);

        var removed = await command.ExecuteNonQueryAsync(ct);

        // Detach unconditionally, before the no-op early return. TryClaimAsync inserted this row through EF,
        // so it is still tracked as Unchanged; the DELETE above is raw SQL and does not touch the change
        // tracker. Leaving the stale entity behind makes a later re-claim on the same scope throw
        // InvalidOperationException ("already being tracked") — not a DbUpdateException, so it escapes
        // TryClaimAsync's catch instead of being handled as a claim conflict. That hazard is just as real
        // when the DELETE matched nothing, which is why this runs first.
        var tracked = dbContext.ChangeTracker.Entries<InboxMessage>()
            .FirstOrDefault(e => e.Entity.MessageId == messageId.Value && e.Entity.ConsumerType == consumerType);
        if (tracked is not null)
            tracked.State = EntityState.Detached;

        if (removed == 0)
        {
            // Already Processed, or reclaimed by another instance after the stale window — nothing to undo.
            logger.LogInformation(
                "[INBOX] No Processing claim of ours to release for message {MessageId} / {Consumer}",
                messageId.Value, consumerType);
            return;
        }

        logger.LogWarning("[INBOX] Released claim on message {MessageId} for {Consumer} so it can be retried",
            messageId.Value, consumerType);
    }

    private static void AddParameter(DbCommand command, string name, object value, DbType? dbType = null)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        if (dbType is not null) parameter.DbType = dbType.Value;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    /// <summary>
    /// Mark the message as processed after successful consumer execution.
    /// </summary>
    public async Task MarkAsProcessedAsync(Guid? messageId, string consumerType, CancellationToken ct)
    {
        if (messageId is null) return;

        var schema = dbContext.Model.GetDefaultSchema() ?? "dbo";
        await dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE [" + schema + "].[InboxMessage] " +
            "SET Status = 'Processed', ProcessedAt = {0} " +
            "WHERE MessageId = {1} AND ConsumerType = {2}",
            new object[] { dateTimeProvider.ApplicationNow, messageId.Value, consumerType }, ct);
    }
}
