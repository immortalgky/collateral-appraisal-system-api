namespace Workflow.Contracts.FeeAppointmentApprovals;

/// <summary>
/// Read-only cross-module query: asks the Workflow module to evaluate — without side effects —
/// whether a proposed appointment date or a cumulative added-fee total would require bank approval
/// under the currently configured policy.
///
/// Used by Appraisal module inline-edit handlers so edit-time and submit-time verdicts
/// are produced by the same policy service and can never disagree.
/// </summary>
public record EvaluateFeeAppointmentApprovalQuery(
    Guid AppraisalId,
    string RequestSource,
    DateTime? ProposedAppointmentDate,
    int? RescheduleCount,
    decimal? CumulativeAddedFeeTotal)
    : IQuery<EvaluateFeeAppointmentApprovalResult>;

public record EvaluateFeeAppointmentApprovalResult(
    bool AppointmentRequiresApproval,
    bool FeesRequireApproval);
