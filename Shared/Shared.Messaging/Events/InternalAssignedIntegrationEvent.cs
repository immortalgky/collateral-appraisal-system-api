namespace Shared.Messaging.Events;

public record InternalAssignedIntegrationEvent : IntegrationEvent
{
    public Guid AppraisalId { get; init; }
    public string AssigneeUserId { get; init; } = default!;
    public string? InternalAppraiserId { get; init; }
    public string AssignmentMethod { get; init; } = "RoundRobin";
    public string? InternalFollowupAssignmentMethod { get; init; }
    public string? CompletedBy { get; init; }
    public string? AppraisalNumber { get; init; }
}
