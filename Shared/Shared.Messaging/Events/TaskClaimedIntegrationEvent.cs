namespace Shared.Messaging.Events;

public record TaskClaimedIntegrationEvent : IntegrationEvent
{
    public Guid CorrelationId { get; init; }
    public string PoolGroup { get; init; } = default!;
    public string ClaimedBy { get; init; } = default!;
    public DateTime AssignedAt { get; init; }
}
