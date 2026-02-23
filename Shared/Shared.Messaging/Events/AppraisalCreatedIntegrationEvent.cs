namespace Shared.Messaging.Events;

public record AppraisalCreatedIntegrationEvent : IntegrationEvent
{
    public Guid RequestId { get; init; }
    public string? RequestNumber { get; init; }
    public string? ExternalCaseKey { get; init; }
    public string? ExternalSystem { get; init; }
    public DateTime CreatedAt { get; init; }
}
