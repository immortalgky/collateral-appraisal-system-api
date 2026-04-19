namespace Shared.Messaging.Events;

public record AppraisalCancelIntegrationEvent : IntegrationEvent
{
    public Guid AppraisalId { get; init; }
    public string CancelledBy { get; init; } = null!;
    public DateTime CancelledAt { get; init; }
    public string? CancelReason { get; init; }
}
