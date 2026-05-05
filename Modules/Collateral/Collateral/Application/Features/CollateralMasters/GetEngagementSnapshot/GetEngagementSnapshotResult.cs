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
    Guid PropertyId,
    string AppraisalType,
    DateTime AppraisalDate,
    decimal? AppraisedValue,
    string? AppraiserUserId,
    Guid? AppraisalCompanyId,
    string? AppraisalCompanyName,
    DateTime CreatedOn,
    /// <summary>Raw JSON snapshot — returned as-is for the FE to parse.</summary>
    string Snapshot
);
