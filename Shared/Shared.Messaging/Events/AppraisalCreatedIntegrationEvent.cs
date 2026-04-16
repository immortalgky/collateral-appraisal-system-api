namespace Shared.Messaging.Events;

public record AppraisalCreatedIntegrationEvent : IntegrationEvent
{
    public Guid AppraisalId { get; init; }
    public Guid RequestId { get; init; }
    public string? AppraisalNumber { get; init; }
    public string? AppraisalType { get; init; }
    public string? CreatedBy { get; init; }
    public string? RequestedBy { get; init; }
    public DateTime CreatedAt { get; init; }
    public bool IsPma { get; init; }
    public decimal? FacilityLimit { get; init; }
    public string? Priority { get; init; }
    public bool HasAppraisalBook { get; init; }
    public string? Channel { get; init; }
}