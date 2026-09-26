using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Integration.Domain.WebhookSubscriptions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Shared.Security;

namespace Integration.Application.Services;

/// <summary>
/// Bespoke LOS token exchange (per the LOS Web API spec — POST + JSON body, NOT standard OAuth2
/// client-credentials form-encoding, no grant_type): POST {TokenEndpoint} with
/// { client_id, client_secret } -&gt; { access_token, token_type, expires_in }.
/// Cached in IMemoryCache keyed by subscription id for (expires_in - margin) so a burst of PMA
/// saves within the token lifetime does not re-hit LOS. Cache idiom: Auth/MenuTreeCache.
/// </summary>
public class LosTokenProvider(
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    ColumnSecretCipher cipher,
    ILogger<LosTokenProvider> logger) : IWebhookTokenProvider
{
    public const string HttpClientName = "WebhookToken";

    // Margin subtracted from the LOS-reported expires_in so a token nearing expiry is refreshed
    // proactively instead of being used right up to (and failing at) the wire.
    private const int ExpiryMarginSeconds = 60;
    private const int MinCacheSeconds = 5;

    private static string CacheKey(Guid subscriptionId) => $"integration:webhook-token:{subscriptionId}";

    public async Task<(string TokenType, string AccessToken)> GetTokenAsync(
        WebhookSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKey(subscription.Id);
        var fingerprint = Fingerprint(subscription);

        // A token is reused only while the settings it was issued for are unchanged. Comparing a
        // fingerprint (rather than invalidating on update) also covers the other app node, whose
        // IMemoryCache the updating node cannot reach.
        if (cache.TryGetValue(cacheKey, out CachedToken? cached) && cached is not null &&
            cached.Fingerprint == fingerprint)
            return (cached.TokenType, cached.AccessToken);

        if (string.IsNullOrWhiteSpace(subscription.TokenEndpoint) ||
            string.IsNullOrWhiteSpace(subscription.ClientId) ||
            string.IsNullOrWhiteSpace(subscription.ClientSecret))
        {
            throw new InvalidOperationException(
                $"Webhook subscription {subscription.Id} ({subscription.SystemCode}) is missing " +
                "TokenEndpoint/ClientId/ClientSecret required for AuthType=TokenBearer.");
        }

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, subscription.TokenEndpoint)
        {
            Content = JsonContent.Create(
                new LosTokenRequest(subscription.ClientId, cipher.Unprotect(subscription.ClientSecret)))
        };

        var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<LosTokenResponse>(cancellationToken)
                   ?? throw new InvalidOperationException(
                       $"Empty token response from {subscription.TokenEndpoint} for subscription {subscription.Id}.");

        // Guard against a null/empty token_type — AuthenticationHeaderValue throws on a null
        // scheme, and the LOS spec doesn't guarantee the field is always populated.
        var tokenType = string.IsNullOrWhiteSpace(body.TokenType) ? "Bearer" : body.TokenType;

        var cacheDuration = TimeSpan.FromSeconds(Math.Max(body.ExpiresIn - ExpiryMarginSeconds, MinCacheSeconds));
        var token = new CachedToken(tokenType, body.AccessToken, fingerprint);
        cache.Set(cacheKey, token, cacheDuration);

        logger.LogInformation(
            "Fetched new LOS token for subscription {SubscriptionId} ({SystemCode}), expires in {ExpiresIn}s",
            subscription.Id, subscription.SystemCode, body.ExpiresIn);

        return (token.TokenType, token.AccessToken);
    }

    public void InvalidateToken(Guid subscriptionId) => cache.Remove(CacheKey(subscriptionId));

    /// <summary>
    /// Hash of everything the token is tied to: where it is fetched, with which credentials, and where
    /// it is sent. The stored (encrypted) ClientSecret changes on every re-entry, so a rotation counts.
    /// </summary>
    private static string Fingerprint(WebhookSubscription s) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{s.TokenEndpoint}\n{s.ClientId}\n{s.ClientSecret}\n{s.CallbackUrl}")));

    private sealed record CachedToken(string TokenType, string AccessToken, string Fingerprint);
}

internal sealed record LosTokenRequest(
    [property: JsonPropertyName("client_id")] string ClientId,
    [property: JsonPropertyName("client_secret")] string ClientSecret);

internal sealed record LosTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);
