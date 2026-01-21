namespace Shared.Messaging.Events;

public record AppraisalStatusChangedIntegrationEvent : IntegrationEvent
{
    public Guid RequestId { get; init; }
    public string? RequestNumber { get; init; }
    public string? ExternalCaseKey { get; init; }
    public string PreviousStatus { get; init; } = null!;
    public string NewStatus { get; init; } = null!;
    public DateTime ChangedAt { get; init; }
}
