using Shared.DDD;

namespace Workflow.Workflow.Models;

public class WorkflowExecutionLog : Entity<Guid>
{
    public Guid WorkflowInstanceId { get; private set; }
    public string? ActivityId { get; private set; }
    public ExecutionLogEvent Event { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public string? Details { get; private set; }
    public string? ActorId { get; private set; }
    public string? CorrelationId { get; private set; }
    public TimeSpan? Duration { get; private set; }
    public string? ErrorMessage { get; private set; }
    public Dictionary<string, object> Metadata { get; private set; } = new();

    public WorkflowInstance WorkflowInstance { get; private set; } = default!;

    private WorkflowExecutionLog()
    {
        // For EF Core
    }

    public static WorkflowExecutionLog Create(
        Guid workflowInstanceId,
        ExecutionLogEvent eventType,
        string? activityId = null,
        string? details = null,
        string? actorId = null,
        string? correlationId = null,
        TimeSpan? duration = null,
        string? errorMessage = null,
        Dictionary<string, object>? metadata = null)
    {
        return new WorkflowExecutionLog
        {
            Id = Guid.NewGuid(),
            WorkflowInstanceId = workflowInstanceId,
            ActivityId = activityId,
            Event = eventType,
            OccurredAt = DateTime.UtcNow,
            Details = details,
            ActorId = actorId,
            CorrelationId = correlationId,
            Duration = duration,
            ErrorMessage = errorMessage,
            Metadata = metadata ?? new Dictionary<string, object>()
        };
    }

    public static WorkflowExecutionLog WorkflowStarted(
        Guid workflowInstanceId,
        string startedBy,
        string? correlationId = null,
        Dictionary<string, object>? metadata = null)
    {
        return Create(workflowInstanceId, ExecutionLogEvent.WorkflowStarted, 
            actorId: startedBy, correlationId: correlationId, metadata: metadata);
    }

    public static WorkflowExecutionLog ActivityStarted(
        Guid workflowInstanceId,
        string activityId,
        string? actorId = null,
        Dictionary<string, object>? metadata = null)
    {
        return Create(workflowInstanceId, ExecutionLogEvent.ActivityStarted, 
            activityId: activityId, actorId: actorId, metadata: metadata);
    }

    public static WorkflowExecutionLog ActivityCompleted(
        Guid workflowInstanceId,
        string activityId,
        string completedBy,
        TimeSpan duration,
        Dictionary<string, object>? metadata = null)
    {
        return Create(workflowInstanceId, ExecutionLogEvent.ActivityCompleted,
            activityId: activityId, actorId: completedBy, duration: duration, metadata: metadata);
    }

    public static WorkflowExecutionLog ActivityFailed(
        Guid workflowInstanceId,
        string activityId,
        string errorMessage,
        string? actorId = null,
        Dictionary<string, object>? metadata = null)
    {
        return Create(workflowInstanceId, ExecutionLogEvent.ActivityFailed,
            activityId: activityId, actorId: actorId, errorMessage: errorMessage, metadata: metadata);
    }

    public static WorkflowExecutionLog WorkflowSuspended(
        Guid workflowInstanceId,
        string reason,
        string? actorId = null)
    {
        return Create(workflowInstanceId, ExecutionLogEvent.WorkflowSuspended,
            details: reason, actorId: actorId);
    }

    public static WorkflowExecutionLog WorkflowResumed(
        Guid workflowInstanceId,
        string resumedBy,
        string? activityId = null)
    {
        return Create(workflowInstanceId, ExecutionLogEvent.WorkflowResumed,
            activityId: activityId, actorId: resumedBy);
    }
}

public enum ExecutionLogEvent
{
    WorkflowStarted,
    WorkflowCompleted,
    WorkflowFailed,
    WorkflowSuspended,
    WorkflowResumed,
    WorkflowCancelled,
    ActivityStarted,
    ActivityCompleted,
    ActivityFailed,
    ActivitySkipped,
    ActivityCancelled,
    BookmarkCreated,
    BookmarkConsumed,
    ExternalCallStarted,
    ExternalCallCompleted,
    ExternalCallFailed,
    RetryAttempted,
    CompensationStarted,
    CompensationCompleted
}