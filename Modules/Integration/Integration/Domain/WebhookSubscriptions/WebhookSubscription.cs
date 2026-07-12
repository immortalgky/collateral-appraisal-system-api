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

        if (authType == WebhookAuthType.TokenBearer)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tokenEndpoint);
            ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
            ArgumentException.ThrowIfNullOrWhiteSpace(clientSecret);
        }
        else
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);
        }

        return new WebhookSubscription(systemCode, callbackUrl, secretKey, authType, tokenEndpoint, clientId,
            clientSecret, httpMethod, eventType);
    }

    public void UpdateCallbackUrl(string callbackUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callbackUrl);
        CallbackUrl = callbackUrl;
    }

    public void UpdateSecretKey(string secretKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);
        SecretKey = secretKey;
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
