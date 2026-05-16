namespace Shared.Messaging.Events;

public record ExternalAppraisalReturnedIntegrationEvent : IntegrationEvent
{
    public Guid AppraisalId { get; init; }
    public Guid AssignmentId { get; init; }
    public Guid CompanyId { get; init; }
    public int CycleNumber { get; init; }
    public DateTime OpenedAt { get; init; }
    public DateTime ClosedAt { get; init; }
    public int BusinessMinutes { get; init; }
}
