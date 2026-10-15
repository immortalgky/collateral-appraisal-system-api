using Shared.Exceptions;

namespace Integration.Domain.WebhookSubscriptions;

public class WebhookSubscription : Aggregate<Guid>
{
    public string SystemCode { get; private set; } = default!;

    // Null = "catch-all" — matches any EventType for this SystemCode. Lets one downstream system
    // (e.g. "LOS") have a default HMAC subscription for its existing 9 outbound event types while a
    // specific event (e.g. "APPRAISAL_PMA_UPDATED") routes to its own dedicated subscription with a
    // different auth model/callback. Uniqueness is enforced on (SystemCode, EventType).
    public string? EventType { get; private set; }

    public string CallbackUrl { get; private set; } = default!;

    // Used only for AuthType = HMAC (existing timestamp + X-Signature headers). Null for
    // TokenBearer subscriptions, whose credential is ClientSecret instead.
    public string? SecretKey { get; private set; }

    public bool IsActive { get; private set; }
    public DateTime? LastDeliveryAt { get; private set; }

    // Auth strategy: "HMAC" (default, existing behavior) or "TokenBearer" (bespoke token-fetch,
    // e.g. LOS's non-OAuth2 POST+JSON token exchange). Kept pluggable so a future external system
    // with yet another auth model can reuse the same delivery/retry machinery.
    public string AuthType { get; private set; } = WebhookAuthType.Hmac;
    public string? TokenEndpoint { get; private set; }
    public string? ClientId { get; private set; }
    public string? ClientSecret { get; private set; }

    // HTTP method used for the callback request. Defaults to "POST" (existing behavior);
    // some external update APIs (e.g. LOS) require PUT.
    public string HttpMethod { get; private set; } = "POST";

    private WebhookSubscription()
    {
    }

    private WebhookSubscription(
        string systemCode,
        string callbackUrl,
        string? secretKey,
        string authType,
        string? tokenEndpoint,
        string? clientId,
        string? clientSecret,
        string httpMethod,
        string? eventType)
    {
        Id = Guid.NewGuid();
        SystemCode = systemCode;
        CallbackUrl = callbackUrl;
        SecretKey = secretKey;
        IsActive = true;
        AuthType = authType;
        TokenEndpoint = tokenEndpoint;
        ClientId = clientId;
        ClientSecret = clientSecret;
        HttpMethod = httpMethod;
        EventType = eventType;
    }

    /// <summary>
    /// Creates a subscription. Existing 3-arg HMAC callers are unaffected — the auth/method/
    /// eventType parameters default to the pre-existing behavior (HMAC + POST + catch-all).
    /// </summary>
    public static WebhookSubscription Create(
        string systemCode,
        string callbackUrl,
        string? secretKey,
        string authType = WebhookAuthType.Hmac,
        string? tokenEndpoint = null,
        string? clientId = null,
        string? clientSecret = null,
        string httpMethod = "POST",
        string? eventType = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(callbackUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(authType);
        ArgumentException.ThrowIfNullOrWhiteSpace(httpMethod);
        EnsureCredentials(authType, tokenEndpoint, clientId, clientSecret, secretKey);

        return new WebhookSubscription(systemCode, callbackUrl, secretKey, authType, tokenEndpoint, clientId,
            clientSecret, httpMethod, eventType);
    }

    /// <summary>
    /// Replaces the connection settings. A null <paramref name="secretKey"/> /
    /// <paramref name="clientSecret"/> keeps the stored one. The credentials of the auth type not in
    /// use are cleared, so a switch never leaves a dormant secret behind.
    /// <para>
    /// The stored ClientSecret is kept only while TokenEndpoint, ClientId and CallbackUrl stay the same.
    /// Otherwise anyone allowed to edit subscriptions could point TokenEndpoint at their own host (and
    /// receive the decrypted secret) or CallbackUrl at it (and receive a freshly minted bearer token) —
    /// bypassing the audited reveal. Re-entering the secret proves they already know it.
    /// </para>
    /// </summary>
    public void Update(
        string callbackUrl,
        string httpMethod,
        string authType,
        string? tokenEndpoint,
        string? clientId,
        string? secretKey,
        string? clientSecret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callbackUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(httpMethod);
        ArgumentException.ThrowIfNullOrWhiteSpace(authType);

        var isTokenBearer = authType == WebhookAuthType.TokenBearer;
        tokenEndpoint = isTokenBearer ? tokenEndpoint : null;
        clientId = isTokenBearer ? clientId : null;

        if (isTokenBearer && clientSecret is null && ClientSecret is not null &&
            (tokenEndpoint != TokenEndpoint || clientId != ClientId || callbackUrl != CallbackUrl))
            throw new DomainException(
                "Re-enter the ClientSecret when changing TokenEndpoint, ClientId or CallbackUrl.");

        // Same for HMAC: redirecting CallbackUrl would hand the new host a stream of
        // (payload, HMAC(secret)) pairs to brute-force the stored SecretKey offline.
        if (!isTokenBearer && secretKey is null && SecretKey is not null && callbackUrl != CallbackUrl)
            throw new DomainException("Re-enter the SecretKey when changing CallbackUrl.");

        clientSecret = isTokenBearer ? clientSecret ?? ClientSecret : null;
        secretKey = isTokenBearer ? null : secretKey ?? SecretKey;
        EnsureCredentials(authType, tokenEndpoint, clientId, clientSecret, secretKey);

        CallbackUrl = callbackUrl;
        HttpMethod = httpMethod;
        AuthType = authType;
        TokenEndpoint = tokenEndpoint;
        ClientId = clientId;
        ClientSecret = clientSecret;
        SecretKey = secretKey;
    }

    private static void EnsureCredentials(
        string authType, string? tokenEndpoint, string? clientId, string? clientSecret, string? secretKey)
    {
        if (authType == WebhookAuthType.TokenBearer)
        {
            if (string.IsNullOrWhiteSpace(tokenEndpoint) || string.IsNullOrWhiteSpace(clientId) ||
                string.IsNullOrWhiteSpace(clientSecret))
                throw new DomainException("TokenBearer needs TokenEndpoint, ClientId and ClientSecret.");
        }
        else if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new DomainException("HMAC needs a SecretKey.");
        }
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public void RecordDelivery(DateTime deliveredAt)
    {
        LastDeliveryAt = deliveredAt;
    }
}

/// <summary>
/// String values for <see cref="WebhookSubscription.AuthType"/>.
/// </summary>
public static class WebhookAuthType
{
    public const string Hmac = "HMAC";
    public const string TokenBearer = "TokenBearer";
}
