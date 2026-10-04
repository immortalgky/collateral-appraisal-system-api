namespace Shared.Data.Outbox;

public class IntegrationEventOutboxMessage
{
    public Guid Id { get; private set; }
    public string EventType { get; private set; } = default!;
    public string Payload { get; private set; } = default!;
    public Dictionary<string, string> Headers { get; private set; } = new();
    public string? CorrelationId { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public string? Error { get; private set; }
    public int RetryCount { get; private set; }
    public OutboxMessageStatus Status { get; private set; }

    private IntegrationEventOutboxMessage() { }

    public static IntegrationEventOutboxMessage Create(
        string eventType,
        string payload,
        DateTime occurredAt,
        string? correlationId = null,
        Dictionary<string, string>? headers = null)
    {
        return new IntegrationEventOutboxMessage
        {
            Id = Guid.CreateVersion7(),
            EventType = eventType,
            Payload = payload,
            CorrelationId = correlationId,
            Headers = headers ?? new Dictionary<string, string>(),
            OccurredAt = occurredAt,
            Status = OutboxMessageStatus.Pending,
            RetryCount = 0
        };
    }

    public void MarkAsProcessing()
    {
        Status = OutboxMessageStatus.Processing;
    }

    public void MarkAsPending()
    {
        Status = OutboxMessageStatus.Pending;
    }

    public void MarkAsProcessed(DateTime processedAt)
    {
        Status = OutboxMessageStatus.Processed;
        ProcessedAt = processedAt;
        Error = null;
    }

    public void IncrementRetryCount(string error, int maxRetries)
    {
        RetryCount++;
        Error = Truncate(error);
        Status = RetryCount >= maxRetries ? OutboxMessageStatus.Failed : OutboxMessageStatus.Pending;
    }

    /// <summary>Deterministic failure (disallowed type, bad payload) — fails immediately, no retries burned.</summary>
    public void MarkAsFailed(string error)
    {
        Status = OutboxMessageStatus.Failed;
        Error = Truncate(error);
    }

    private const int MaxErrorLength = 2000;

    private static string Truncate(string error) => error.Length > MaxErrorLength ? error[..MaxErrorLength] : error;
}

public enum OutboxMessageStatus
{
    Pending,
    Processing,
    Processed,
    Failed
}
