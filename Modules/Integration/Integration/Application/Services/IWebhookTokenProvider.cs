using Integration.Domain.WebhookSubscriptions;

namespace Integration.Application.Services;

/// <summary>
/// Fetches and caches bearer tokens for webhook subscriptions with AuthType = TokenBearer
/// (e.g. the LOS bespoke token exchange). Pluggable so a future external system with a different
/// token-fetch model can implement its own provider without touching WebhookService.
/// </summary>
public interface IWebhookTokenProvider
{
    /// <summary>
    /// Returns (tokenType, accessToken) for the given subscription, using a cached token when
    /// still valid. Use <see cref="InvalidateToken"/> first (e.g. after a 401 from the downstream
    /// system) to force a refetch on the next call.
    /// </summary>
    Task<(string TokenType, string AccessToken)> GetTokenAsync(
        WebhookSubscription subscription,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops the cached token for a subscription, e.g. after the downstream system returns 401.
    /// </summary>
    void InvalidateToken(Guid subscriptionId);
}
