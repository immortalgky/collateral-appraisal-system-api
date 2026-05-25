namespace Shared.Messaging.Events;

/// <summary>
/// Published by the Workflow module when <c>ApprovalActivity</c> resolves with decision == "approve",
/// regardless of which kind of committee (sub-committee, committee, etc.) cast the deciding vote.
/// Not published by <c>MeetingActivity</c> — meeting-based approvals have their own flow.
/// Consumers:
/// - Appraisal module: stamps CompletedAt + ApprovedByCommittee on the aggregate.
/// - Workflow Meetings module: enqueues an <c>AppraisalAcknowledgementQueueItem</c>
///   when the committee code maps to an acknowledgement group.
/// </summary>
public record AppraisalApprovedIntegrationEvent : IntegrationEvent
{
    public Guid AppraisalId { get; init; }
    public string CommitteeCode { get; init; } = null!;
    public string? CommitteeName { get; init; }
    public DateTime ApprovedAt { get; init; }
    public string? ApprovedBy { get; init; } // user that cast the deciding vote

    /// <summary>Appraisal number (e.g. "2568-0001"). Nullable — may not be set in all scenarios.</summary>
    public string? AppraisalNo { get; init; }

    /// <summary>Id of the committee that approved. Resolved by the publisher via committee repository lookup.</summary>
    public Guid CommitteeId { get; init; }

    /// <summary>Vote tally at the time the deciding approval was reached.</summary>
    public int VotesApprove { get; init; }
    public int VotesReject { get; init; }
    public int VotesRouteBack { get; init; }

    /// <summary>
    /// Id of the decision meeting for committee-with-meeting (tier 3) approvals, read from the
    /// preceding MeetingActivity's workflow variable. Null for direct approvals (tiers 1 &amp; 2).
    /// </summary>
    public Guid? DecisionMeetingId { get; init; }
}
