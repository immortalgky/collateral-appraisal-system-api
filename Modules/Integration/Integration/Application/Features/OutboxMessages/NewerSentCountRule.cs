using Integration.Infrastructure;
using Shared.Data.Outbox;

namespace Integration.Application.Features.OutboxMessages;

/// <summary>
/// <c>newerSentCount</c> counts Processed siblings, but <see cref="OutboxCleanupJob{TDbContext}"/> deletes
/// Processed rows after <c>ProcessedRetentionDays</c> while Failed rows live far longer. For a row older than that
/// window "no newer sent sibling" can simply mean "purged", so the count is unknowable (null) rather than a
/// misleading 0.
/// </summary>
public static class NewerSentCountRule
{
    public static bool HistoryMayBePurged(DateTime occurredAt, DateTime now) =>
        occurredAt < now.AddDays(-OutboxDeliveryPolicy.ProcessedRetentionDays);
}
