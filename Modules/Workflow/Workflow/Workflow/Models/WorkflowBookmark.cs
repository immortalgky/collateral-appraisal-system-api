using Shared.DDD;

namespace Workflow.Workflow.Models;

public class WorkflowBookmark : Entity<Guid>
{
    public Guid WorkflowInstanceId { get; private set; }
    public string ActivityId { get; private set; } = default!;
    public string? CorrelationId { get; private set; }
    public BookmarkType Type { get; private set; }
    public string Key { get; private set; } = default!;
    public string? Payload { get; private set; }
    public bool IsConsumed { get; private set; }
    public DateTime? DueAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ConsumedAt { get; private set; }
    public string? ConsumedBy { get; private set; }
    
    // Claim fields for atomic queue processing
    public string? ClaimedBy { get; private set; }
    public DateTime? ClaimedAt { get; private set; }
    public DateTime? LeaseExpiresAt { get; private set; }
    
    public byte[] ConcurrencyToken { get; private set; } = default!;

    public WorkflowInstance WorkflowInstance { get; private set; } = default!;

    private WorkflowBookmark()
    {
        // For EF Core
    }

    public static WorkflowBookmark Create(
        Guid workflowInstanceId,
        string activityId,
        BookmarkType type,
        string key,
        string? correlationId = null,
        string? payload = null,
        DateTime? dueAt = null)
    {
        return new WorkflowBookmark
        {
            Id = Guid.CreateVersion7(),
            WorkflowInstanceId = workflowInstanceId,
            ActivityId = activityId,
            CorrelationId = correlationId,
            Type = type,
            Key = key,
            Payload = payload,
            IsConsumed = false,
            DueAt = dueAt,
            CreatedAt = DateTime.UtcNow,
            ClaimedBy = null,
            ClaimedAt = null,
            LeaseExpiresAt = null
        };
    }

    public void Consume(string consumedBy)
    {
        if (IsConsumed)
            throw new InvalidOperationException("Bookmark has already been consumed");

        IsConsumed = true;
        ConsumedAt = DateTime.UtcNow;
        ConsumedBy = consumedBy;
    }

    public bool IsDue()
    {
        return DueAt.HasValue && DueAt.Value <= DateTime.UtcNow;
    }

    public bool IsExpired(TimeSpan? expiration = null)
    {
        if (!expiration.HasValue) return false;
        return CreatedAt.Add(expiration.Value) <= DateTime.UtcNow;
    }

    public void Claim(string claimedBy, TimeSpan leaseDuration)
    {
        if (IsConsumed)
            throw new InvalidOperationException("Cannot claim a consumed bookmark");
        
        if (ClaimedBy != null && !IsLeaseExpired())
            throw new InvalidOperationException($"Bookmark is already claimed by {ClaimedBy}");

        ClaimedBy = claimedBy;
        ClaimedAt = DateTime.UtcNow;
        LeaseExpiresAt = DateTime.UtcNow.Add(leaseDuration);
    }

    public void ReleaseClaim()
    {
        ClaimedBy = null;
        ClaimedAt = null;
        LeaseExpiresAt = null;
    }

    public bool IsLeaseExpired()
    {
        return LeaseExpiresAt.HasValue && LeaseExpiresAt.Value <= DateTime.UtcNow;
    }

    public bool IsAvailableForClaim()
    {
        return !IsConsumed && (ClaimedBy == null || IsLeaseExpired());
    }
}

public enum BookmarkType
{
    UserAction,
    Timer,
    ExternalMessage,
    ManualIntervention,
    Approval
}