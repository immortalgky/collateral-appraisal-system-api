namespace Integration.Domain.WebhookDeliveries;

public class WebhookDelivery : Entity<Guid>
{
    public Guid SubscriptionId { get; private set; }
    public string EventType { get; private set; } = default!;
    public string Payload { get; private set; } = default!;
    public string Status { get; private set; } = default!;
    public int AttemptCount { get; private set; }
    public int? LastStatusCode { get; private set; }
    public string? LastError { get; private set; }
    public DateTime? DeliveredAt { get; private set; }

    private WebhookDelivery()
    {
    }

    private WebhookDelivery(
        Guid subscriptionId,
        string eventType,
        string payload)
    {
        Id = Guid.NewGuid();
        SubscriptionId = subscriptionId;
        EventType = eventType;
        Payload = payload;
        Status = DeliveryStatus.Pending;
        AttemptCount = 0;
    }

    public static WebhookDelivery Create(
        Guid subscriptionId,
        string eventType,
        string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        return new WebhookDelivery(subscriptionId, eventType, payload);
    }

    public void BeginRetry()
    {
        if (Status != DeliveryStatus.Failed)
            throw new InvalidOperationException("Only failed deliveries can be retried.");

        Status = DeliveryStatus.Pending;
        LastStatusCode = null;
        LastError = null;
        DeliveredAt = null;
    }

    public void RecordSuccess(int statusCode, int attempts, DateTime deliveredAt)
    {
        AttemptCount += attempts;
        LastStatusCode = statusCode;
        LastError = null;
        Status = DeliveryStatus.Delivered;
        DeliveredAt = deliveredAt;
    }

    public void RecordFailure(int statusCode, int attempts, string? error)
    {
        AttemptCount += attempts;
        LastStatusCode = statusCode;
        LastError = error;
        Status = DeliveryStatus.Failed;
    }
}

public static class DeliveryStatus
{
    public const string Pending = "Pending";
    public const string Delivered = "Delivered";
    public const string Failed = "Failed";
}
