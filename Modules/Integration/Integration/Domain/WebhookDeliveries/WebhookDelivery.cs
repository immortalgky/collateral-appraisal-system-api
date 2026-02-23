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
    public DateTime? NextRetryAt { get; private set; }
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

    public void RecordAttempt(int statusCode, string? error = null)
    {
        AttemptCount++;
        LastStatusCode = statusCode;
        LastError = error;

        if (statusCode >= 200 && statusCode < 300)
        {
            Status = DeliveryStatus.Delivered;
            DeliveredAt = DateTime.UtcNow;
            NextRetryAt = null;
        }
        else if (AttemptCount >= 3)
        {
            Status = DeliveryStatus.Failed;
            NextRetryAt = null;
        }
        else
        {
            Status = DeliveryStatus.Retrying;
            NextRetryAt = DateTime.UtcNow.Add(GetRetryDelay(AttemptCount));
        }
    }

    private static TimeSpan GetRetryDelay(int attemptCount)
    {
        return attemptCount switch
        {
            1 => TimeSpan.FromSeconds(30),
            2 => TimeSpan.FromMinutes(5),
            _ => TimeSpan.FromMinutes(30)
        };
    }
}

public static class DeliveryStatus
{
    public const string Pending = "Pending";
    public const string Retrying = "Retrying";
    public const string Delivered = "Delivered";
    public const string Failed = "Failed";
}
