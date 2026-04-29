namespace Shared.Messaging.Events;

public record AppraisalActivityTransitionedIntegrationEvent : IntegrationEvent
{
    public Guid AppraisalId { get; init; }
    public string ActivityId { get; init; } = default!;
    public string? Movement { get; init; }
    public DateTime OccurredAt { get; init; }
}
