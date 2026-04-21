namespace Shared.Messaging.Events;

public record InternalFollowupAssignedIntegrationEvent : IntegrationEvent
{
    public Guid AppraisalId { get; init; }
    public string InternalAppraiserId { get; init; } = default!;
    public string InternalFollowupAssignmentMethod { get; init; } = default!;
    public string? CompletedBy { get; init; }
}
