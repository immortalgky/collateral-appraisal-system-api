namespace Integration.Domain.WebhookSubscriptions;

public class WebhookSubscription : Aggregate<Guid>
{
    public string SystemCode { get; private set; } = default!;
    public string CallbackUrl { get; private set; } = default!;
    public string SecretKey { get; private set; } = default!;
    public bool IsActive { get; private set; }
    public DateTime? LastDeliveryAt { get; private set; }

    private WebhookSubscription()
    {
    }

    private WebhookSubscription(
        string systemCode,
        string callbackUrl,
        string secretKey)
    {
        Id = Guid.NewGuid();
        SystemCode = systemCode;
        CallbackUrl = callbackUrl;
        SecretKey = secretKey;
        IsActive = true;
    }

    public static WebhookSubscription Create(
        string systemCode,
        string callbackUrl,
        string secretKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(callbackUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);

        return new WebhookSubscription(systemCode, callbackUrl, secretKey);
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
