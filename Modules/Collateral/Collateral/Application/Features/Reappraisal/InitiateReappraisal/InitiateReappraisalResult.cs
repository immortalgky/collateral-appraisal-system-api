namespace Collateral.Application.Features.Reappraisal.InitiateReappraisal;

/// <summary>
/// Result of the InitiateReappraisal command — shown in the success popup.
/// </summary>
public record InitiateReappraisalResult(
    string GroupNumber,
    List<Guid> CreatedRequestIds,
    List<SkippedReappraisalItem> Skipped
);

/// <summary>
/// An appraisal that was skipped during initiation because a non-terminal
/// reappraisal Request already references it (Layer 1 dedupe).
/// </summary>
public record SkippedReappraisalItem(
    Guid AppraisalId,
    string? OldAppraisalReportNumber,
    string Reason
);
