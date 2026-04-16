namespace Shared.Messaging.Events;

public record TaskCompletedIntegrationEvent : IntegrationEvent
{
    public Guid CorrelationId { get; init; }
    public string TaskName { get; init; } = default!;
    public string ActionTaken { get; init; } = default!;
    public string? CompletedBy { get; init; }
    public string? WorkflowInstanceName { get; init; }
    public string? AppraisalNumber { get; init; }
    public DateTime AssignedAt { get; init; }
    public bool WasStarted { get; init; }
    public bool WasOverdue { get; init; }
    public string OriginalAssignedTo { get; init; } = default!;
}
