namespace Shared.Messaging.Events;

public record InternalFollowupAssignedIntegrationEvent : IntegrationEvent
{
    public Guid AppraisalId { get; init; }

    /// <summary>Null when the selection found nobody (no_match): the company's email still goes out (#622).</summary>
    public string? InternalAppraiserId { get; init; }

    public string? InternalFollowupAssignmentMethod { get; init; }
    public string? CompletedBy { get; init; }
}
