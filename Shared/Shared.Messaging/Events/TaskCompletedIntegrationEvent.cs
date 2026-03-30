namespace Shared.Messaging.Events;

public record TaskCompletedIntegrationEvent
{
    public Guid CorrelationId { get; init; }
    public string TaskName { get; init; } = default!;
    public string ActionTaken { get; init; } = default!;
    public string? CompletedBy { get; init; }
    public string? WorkflowInstanceName { get; init; }
}
