using Shared.DDD;

namespace Workflow.Workflow.Models;

public class WorkflowExternalCall : Entity<Guid>
{
    public Guid WorkflowInstanceId { get; private set; }
    public string ActivityId { get; private set; } = default!;
    public ExternalCallType Type { get; private set; }
    public string Endpoint { get; private set; } = default!;
    public string Method { get; private set; } = default!;
    public string? RequestPayload { get; private set; }
    public Dictionary<string, string> Headers { get; private set; } = new();
    public string IdempotencyKey { get; private set; } = default!;
    public ExternalCallStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? ResponsePayload { get; private set; }
    public int AttemptCount { get; private set; }
    public string? ErrorMessage { get; private set; }
    public TimeSpan? Duration { get; private set; }
    public byte[] ConcurrencyToken { get; private set; } = default!;

    public WorkflowInstance WorkflowInstance { get; private set; } = default!;

    private WorkflowExternalCall()
    {
        // For EF Core
    }

    public static WorkflowExternalCall Create(
        Guid workflowInstanceId,
        string activityId,
        ExternalCallType type,
        string endpoint,
        string method,
        string? requestPayload = null,
        Dictionary<string, string>? headers = null)
    {
        return new WorkflowExternalCall
        {
            Id = Guid.NewGuid(),
            WorkflowInstanceId = workflowInstanceId,
            ActivityId = activityId,
            Type = type,
            Endpoint = endpoint,
            Method = method,
            RequestPayload = requestPayload,
            Headers = headers ?? new Dictionary<string, string>(),
            IdempotencyKey = $"{workflowInstanceId}:{activityId}:{Guid.NewGuid()}",
            Status = ExternalCallStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            AttemptCount = 0
        };
    }

    public void MarkAsStarted()
    {
        Status = ExternalCallStatus.InProgress;
        StartedAt = DateTime.UtcNow;
        AttemptCount++;
    }

    public void MarkAsCompleted(string responsePayload, TimeSpan duration)
    {
        Status = ExternalCallStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        ResponsePayload = responsePayload;
        Duration = duration;
        ErrorMessage = null;
    }

    public void MarkAsFailed(string errorMessage, TimeSpan duration)
    {
        Status = ExternalCallStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        ErrorMessage = errorMessage;
        Duration = duration;
    }

    public void MarkAsTimedOut(TimeSpan duration)
    {
        Status = ExternalCallStatus.TimedOut;
        CompletedAt = DateTime.UtcNow;
        ErrorMessage = "Request timed out";
        Duration = duration;
    }

    public bool CanRetry(int maxRetries)
    {
        return AttemptCount < maxRetries && 
               (Status == ExternalCallStatus.Failed || Status == ExternalCallStatus.TimedOut);
    }

    public void ResetForRetry()
    {
        Status = ExternalCallStatus.Pending;
        StartedAt = null;
        CompletedAt = null;
        ErrorMessage = null;
        Duration = null;
        // Keep AttemptCount for retry logic
    }

    public bool IsCompleted()
    {
        return Status == ExternalCallStatus.Completed || 
               Status == ExternalCallStatus.Failed || 
               Status == ExternalCallStatus.TimedOut;
    }

    public void AddHeader(string key, string value)
    {
        Headers[key] = value;
    }
}

public enum ExternalCallType
{
    HttpRequest,
    DatabaseQuery,
    FileOperation,
    ThirdPartyApi,
    EmailService,
    NotificationService
}

public enum ExternalCallStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    TimedOut
}