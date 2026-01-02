namespace Workflow.Workflow.Events;

// Workflow events for MassTransit
public record WorkflowStarted
{
    public Guid WorkflowInstanceId { get; init; }
    public Guid WorkflowDefinitionId { get; init; }
    public string InstanceName { get; init; } = default!;
    public string StartedBy { get; init; } = default!;
    public DateTime StartedAt { get; init; }
    public string? CorrelationId { get; init; }
}

public record WorkflowActivityCompleted
{
    public Guid WorkflowInstanceId { get; init; }
    public string ActivityId { get; init; } = default!;
    public string CompletedBy { get; init; } = default!;
    public DateTime CompletedAt { get; init; }
    public Dictionary<string, object> OutputData { get; init; } = new();
    public string? Comments { get; init; }
}

public record WorkflowCancelled
{
    public Guid WorkflowInstanceId { get; init; }
    public string CancelledBy { get; init; } = default!;
    public DateTime CancelledAt { get; init; }
    public string Reason { get; init; } = default!;
}