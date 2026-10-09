namespace Shared.Messaging.Filters;

/// <summary>
/// Shared stale-claim window for <see cref="InboxGuard{TDbContext}"/> (docs/failed-messages/api-contract.md) — a consumer that throws without going through <see cref="InboxGuard{TDbContext}.RunOnceAsync"/>'s
/// release path leaves its inbox claim row Processing for up to this long. Retrying a failed message
/// before the window elapses risks the redelivered message landing on a still-Processing inbox claim and
/// being silently skipped as a duplicate. Public (rather than a private const on the generic
/// <see cref="InboxGuard{TDbContext}"/>) so the Failed Messages retry handler and its DTO mapping (design
/// `TooSoon` skip reason / `retryAvailableAt`) can reference the exact same value.
/// </summary>
public static class InboxGuardPolicy
{
    public static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(5);
}
