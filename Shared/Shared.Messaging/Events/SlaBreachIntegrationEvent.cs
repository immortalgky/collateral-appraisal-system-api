namespace Shared.Messaging.Events;

public record SlaBreachIntegrationEvent : IntegrationEvent
{
    public Guid CorrelationId { get; init; }
    public Guid PendingTaskId { get; init; }
    public string TaskName { get; init; } = default!;
    public string AssignedTo { get; init; } = default!;
    public string SlaStatus { get; init; } = default!;
    public DateTime DueAt { get; init; }
    public DateTime DetectedAt { get; init; }
    public DateTime AssignedAt { get; init; }
}
