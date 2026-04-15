namespace Shared.Messaging.Events;

public record TaskStartedIntegrationEvent : IntegrationEvent
{
    public Guid CorrelationId { get; init; }
    public string AssignedTo { get; init; } = default!;
    public DateTime AssignedAt { get; init; }
    public string? PreviousAssignedTo { get; init; }  // non-null only for pool tasks
}
