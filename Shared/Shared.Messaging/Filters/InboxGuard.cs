using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Data.Outbox;

namespace Shared.Messaging.Filters;

public class InboxGuard<TDbContext>(
    TDbContext dbContext,
    ILogger<InboxGuard<TDbContext>> logger)
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
            var inbox = InboxMessage.Create(id, consumerType);
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
        if (existing.StartedAt < DateTime.Now.AddMinutes(-StaleThresholdMinutes))
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
                var inbox = InboxMessage.Create(id, consumerType);
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
            new object[] { DateTime.Now, messageId.Value, consumerType }, ct);
    }
}
