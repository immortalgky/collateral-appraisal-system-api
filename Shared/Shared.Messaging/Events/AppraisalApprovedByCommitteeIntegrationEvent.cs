namespace Shared.Messaging.Events;

/// <summary>
/// Published by the Workflow module when the pending-approval activity completes
/// with decision == "approve". Consumed by the Appraisal module to stamp
/// CompletedAt and ApprovedByCommittee on the aggregate.
/// </summary>
public record AppraisalApprovedByCommitteeIntegrationEvent : IntegrationEvent
{
    public Guid AppraisalId { get; init; }
    public string CommitteeCode { get; init; } = null!;
    public string? CommitteeName { get; init; }
    public DateTime ApprovedAt { get; init; }
    public string? ApprovedBy { get; init; } // user that cast the deciding vote
}
