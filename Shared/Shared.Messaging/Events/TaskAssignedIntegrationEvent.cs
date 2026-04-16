namespace Shared.Messaging.Events;

public record TaskAssignedIntegrationEvent : IntegrationEvent
{
    public Guid CorrelationId { get; init; }
    public string TaskName { get; init; } = default!;
    public string AssignedTo { get; init; } = default!;
    public string AssignedType { get; init; } = default!;
    public string? CompletedBy { get; init; }
    public string? StartedBy { get; init; }
    public string? WorkflowInstanceName { get; init; }
    public string? AppraisalNumber { get; init; }
    public DateTime AssignedAt { get; init; }
}
