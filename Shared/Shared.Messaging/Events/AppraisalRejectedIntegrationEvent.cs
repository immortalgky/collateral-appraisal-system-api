namespace Shared.Messaging.Events;

/// <summary>
/// Published by the Workflow module when <c>ApprovalActivity</c> resolves with decision == "reject"
/// (after any <c>decisionConditions</c> remap). Symmetric to <see cref="AppraisalApprovedIntegrationEvent"/>.
/// Consumers:
/// - Collateral module: spools a <c>PendingCollateralResult</c> row so the next export run emits
///   a status-R record to the AS400 Collateral Result interface.
/// </summary>
public record AppraisalRejectedIntegrationEvent : IntegrationEvent
{
    public Guid AppraisalId { get; init; }

    /// <summary>Appraisal number (e.g. "2568-0001"). Nullable — may not be set in all scenarios.</summary>
    public string? AppraisalNo { get; init; }

    public DateTime RejectedAt { get; init; }

    /// <summary>User that cast the deciding vote.</summary>
    public string? RejectedBy { get; init; }

    public string CommitteeCode { get; init; } = null!;
    public string? CommitteeName { get; init; }

    /// <summary>Vote tally at the time the rejection decision was reached.</summary>
    public int VotesApprove { get; init; }
    public int VotesReject { get; init; }
    public int VotesRouteBack { get; init; }
}
