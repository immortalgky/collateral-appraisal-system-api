namespace Shared.Messaging.Events;

public record CompanyAssignedIntegrationEvent : IntegrationEvent
{
    public Guid AppraisalId { get; init; }
    public Guid CompanyId { get; init; }
    public string CompanyName { get; init; } = default!;
    public string AssignmentMethod { get; init; } = default!;
    public string? CompletedBy { get; init; }
    public string? AppraisalNumber { get; init; }
}
