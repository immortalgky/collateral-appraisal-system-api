namespace Shared.Messaging.Events;

public record AppraisalCreatedIntegrationEvent : IntegrationEvent
{
    public Guid AppraisalId { get; init; }
    public Guid RequestId { get; init; }
    public string? AppraisalNumber { get; init; }
    public string? AppraisalType { get; init; }
    public string? CreatedBy { get; init; }
    public DateTime CreatedAt { get; init; }
}