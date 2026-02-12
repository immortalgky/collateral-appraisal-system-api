using Shared.DDD;

namespace Workflow.Workflow.Models;

public class WorkflowOutbox : Entity<Guid>
{
    public DateTime OccurredAt { get; private set; }
    public string Type { get; private set; } = default!;
    public string Payload { get; private set; } = default!;
    public Dictionary<string, string> Headers { get; private set; } = new();
    public int Attempts { get; private set; }
    public DateTime? NextAttemptAt { get; private set; }
    public OutboxStatus Status { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? CorrelationId { get; private set; }
    public Guid? WorkflowInstanceId { get; private set; }
    public string? ActivityId { get; private set; }
    public byte[] ConcurrencyToken { get; private set; } = default!;

    public WorkflowInstance? WorkflowInstance { get; private set; }

    private WorkflowOutbox()
    {
        // For EF Core
    }

    public static WorkflowOutbox Create(
        string eventType,
        string payload,
        Dictionary<string, string>? headers = null,
        string? correlationId = null,
        Guid? workflowInstanceId = null,
        string? activityId = null)
    {
        return new WorkflowOutbox
        {
            Id = Guid.CreateVersion7(),
            OccurredAt = DateTime.UtcNow,
            Type = eventType,
            Payload = payload,
            Headers = headers ?? new Dictionary<string, string>(),
            Attempts = 0,
            Status = OutboxStatus.Pending,
            CorrelationId = correlationId,
            WorkflowInstanceId = workflowInstanceId,
            ActivityId = activityId
        };
    }

    public void MarkAsProcessing()
    {
        Status = OutboxStatus.Processing;
        Attempts++;
        NextAttemptAt = null;
    }

    public void MarkAsProcessed()
    {
        Status = OutboxStatus.Processed;
        ProcessedAt = DateTime.UtcNow;
        NextAttemptAt = null;
        ErrorMessage = null;
    }

    public void MarkAsFailed(string errorMessage, TimeSpan? retryDelay = null)
    {
        Status = OutboxStatus.Failed;
        ErrorMessage = errorMessage;
        
        if (retryDelay.HasValue)
        {
            NextAttemptAt = DateTime.UtcNow.Add(retryDelay.Value);
        }
    }

    public void IncrementAttempt(string errorMessage)
    {
        Attempts++;
        Status = OutboxStatus.Failed;
        ErrorMessage = errorMessage;
        NextAttemptAt = DateTime.UtcNow.Add(CalculateRetryDelay());
    }

    public void ScheduleRetry(TimeSpan delay)
    {
        Status = OutboxStatus.Pending;
        NextAttemptAt = DateTime.UtcNow.Add(delay);
    }

    public void MarkAsDeadLetter(string reason)
    {
        Status = OutboxStatus.DeadLetter;
        ErrorMessage = reason;
        NextAttemptAt = null;
    }

    public bool IsReadyForProcessing()
    {
        return Status == OutboxStatus.Pending && 
               (NextAttemptAt == null || NextAttemptAt <= DateTime.UtcNow);
    }

    public bool ShouldRetry(int maxRetries)
    {
        return Attempts < maxRetries && Status == OutboxStatus.Failed;
    }

    public TimeSpan CalculateRetryDelay(int baseDelaySeconds = 5)
    {
        // Exponential backoff: 5s, 10s, 20s, 40s, 80s, etc.
        var delaySeconds = baseDelaySeconds * Math.Pow(2, Math.Min(Attempts, 10));
        return TimeSpan.FromSeconds(delaySeconds);
    }

    public void AddHeader(string key, string value)
    {
        Headers[key] = value;
    }

    public string? GetHeader(string key)
    {
        return Headers.TryGetValue(key, out var value) ? value : null;
    }
}

public enum OutboxStatus
{
    Pending,
    Processing,
    Processed,
    Failed,
    DeadLetter
}