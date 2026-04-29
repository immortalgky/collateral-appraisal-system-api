namespace Shared.Messaging.Events;

public record AppraisalCompletedIntegrationEvent : IntegrationEvent
{
    public Guid AppraisalId { get; init; }
    public Guid RequestId { get; init; }
    public string? RequestNumber { get; init; }
    public string? ExternalCaseKey { get; init; }
    public DateTime CompletedAt { get; init; }
}
