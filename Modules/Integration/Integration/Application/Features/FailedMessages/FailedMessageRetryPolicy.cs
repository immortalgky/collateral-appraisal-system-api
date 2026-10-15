using Shared.Messaging.Filters;

namespace Integration.Application.Features.FailedMessages;

/// <summary>
/// Per docs/failed-messages/api-contract.md: a row is not retryable until InboxGuard's stale-claim
/// window has elapsed — republishing earlier risks the redelivered message landing on a
/// still-Processing inbox claim and being silently skipped. The window is measured from
/// max(FaultedAt, CollectedAt): FaultedAt can fall back to the PUBLISH time for an Error row with no
/// parseable MT-Fault-Timestamp, while CollectedAt is always on or after the real fault, so taking the
/// later of the two is conservative. One place for the retry handler's `TooSoon` check and the
/// list/detail DTO's `retryAvailableAt` to agree.
/// </summary>
public static class FailedMessageRetryPolicy
{
    public static DateTime RetryAvailableAt(DateTime faultedAt, DateTime collectedAt) =>
        (faultedAt > collectedAt ? faultedAt : collectedAt) + InboxGuardPolicy.StaleThreshold;

    public static bool IsTooSoon(DateTime faultedAt, DateTime collectedAt, DateTime now) =>
        now < RetryAvailableAt(faultedAt, collectedAt);

    /// <summary>
    /// `retryAvailableAt` for a list/detail row — omitted (null) when the row isn't Pending, or the
    /// window has already elapsed (retry is available now, nothing to wait for).
    /// </summary>
    public static DateTime? RetryAvailableAtOrNull(string status, DateTime faultedAt, DateTime collectedAt, DateTime now)
    {
        if (status != Domain.FailedMessages.FailedMessageStatus.Pending)
            return null;

        var availableAt = RetryAvailableAt(faultedAt, collectedAt);
        return availableAt > now ? availableAt : null;
    }
}
