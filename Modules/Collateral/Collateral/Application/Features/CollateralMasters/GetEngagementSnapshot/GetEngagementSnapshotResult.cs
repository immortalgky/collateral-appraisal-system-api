namespace Collateral.Application.Features.CollateralMasters.GetEngagementSnapshot;

/// <summary>
/// Full engagement row including the raw Snapshot JSON (returned as a string for FE to parse).
/// </summary>
public record GetEngagementSnapshotResult(
    Guid Id,
    Guid CollateralMasterId,
    Guid AppraisalId,
    string AppraisalNumber,
    Guid RequestId,
    string RequestNumber,
    // PropertyId and AppraisedValue dropped in PR-4: the engagement is per-appraisal, not
    // per-property, and the appraisal-level value now lives on CollateralEngagements.AppraisalValue.
    string AppraisalType,
    DateTime AppraisalDate,
    string? AppraiserUserId,
    Guid? AppraisalCompanyId,
    string? AppraisalCompanyName,
    DateTime CreatedAt,
    /// <summary>Raw JSON snapshot — returned as-is for the FE to parse.</summary>
    string Snapshot
);
