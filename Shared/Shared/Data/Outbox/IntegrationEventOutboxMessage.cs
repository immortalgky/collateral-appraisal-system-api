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

    /// <summary>
    /// When this row last entered <see cref="OutboxMessageStatus.Processing"/>. Cleared when the row
    /// goes back to Pending or is Processed; KEPT when it ends up Failed, as the row's last claim.
    /// Lets the delivery service and <see cref="OutboxCleanupJob{TDbContext}"/> recover a row left
    /// <c>Processing</c> by a crash or ungraceful shutdown, instead of relying on
    /// <see cref="OccurredAt"/> (which can be old for a legitimately in-flight row), and lets the
    /// job age a Failed row by its last claim, not its creation, so a resent row that fails again
    /// is not purged for being old. Only Processing rows are ever reset or reported as stuck, so a
    /// Failed row carrying a value is never misread as orphaned.
    /// </summary>
    public DateTime? ProcessingStartedAt { get; private set; }

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

    public void MarkAsProcessing(DateTime now)
    {
        Status = OutboxMessageStatus.Processing;
        ProcessingStartedAt = now;
    }

    /// <summary>
    /// Puts a row claimed this batch (<see cref="MarkAsProcessing"/>) back without burning a retry —
    /// a group-break left behind by an earlier failure in the same correlation group, or the whole
    /// batch on a shutdown mid-publish.
    /// Clears <see cref="ProcessingStartedAt"/> so it isn't later misread as orphaned/stuck.
    /// </summary>
    public void MarkAsPending()
    {
        Status = OutboxMessageStatus.Pending;
        ProcessingStartedAt = null;
    }

    public void MarkAsProcessed(DateTime processedAt)
    {
        Status = OutboxMessageStatus.Processed;
        ProcessedAt = processedAt;
        ProcessingStartedAt = null;
        Error = null;
    }

    public void IncrementRetryCount(string error, int maxRetries)
    {
        RetryCount++;
        Error = Truncate(error);
        Status = RetryCount >= maxRetries ? OutboxMessageStatus.Failed : OutboxMessageStatus.Pending;
        if (Status == OutboxMessageStatus.Pending)
            ProcessingStartedAt = null;
    }

    /// <summary>Deterministic failure (disallowed type, bad payload) — fails immediately, no retries
    /// burned. Keeps <see cref="ProcessingStartedAt"/> as the row's last claim (see its remarks).</summary>
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
