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
            OccurredAt = DateTime.UtcNow,
            Status = OutboxMessageStatus.Pending,
            RetryCount = 0
        };
    }

    public void MarkAsProcessing()
    {
        Status = OutboxMessageStatus.Processing;
    }

    public void MarkAsProcessed()
    {
        Status = OutboxMessageStatus.Processed;
        ProcessedAt = DateTime.UtcNow;
        Error = null;
    }

    public void IncrementRetryCount(string error, int maxRetries)
    {
        RetryCount++;
        Error = error.Length > 2000 ? error[..2000] : error;
        Status = RetryCount >= maxRetries ? OutboxMessageStatus.Failed : OutboxMessageStatus.Pending;
    }
}

public enum OutboxMessageStatus
{
    Pending,
    Processing,
    Processed,
    Failed
}
