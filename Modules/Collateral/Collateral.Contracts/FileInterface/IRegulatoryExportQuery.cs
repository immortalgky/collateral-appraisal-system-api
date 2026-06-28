namespace Collateral.Contracts.FileInterface;

/// <summary>
/// Returns one row per active IsMaster collateral master for the monthly regulatory snapshot.
/// No sent-ledger: every run is a full re-extract.
/// </summary>
public interface IRegulatoryExportQuery
{
    Task<IReadOnlyList<RegulatoryExportRow>> GetRowsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// One row of the outbound CAS-AS400-Regulatory interface (per active IsMaster collateral master).
/// Carries typed field values that <c>RegulatoryFileWriter</c> formats into a 308-char Detail record.
/// Produced by <c>RegulatoryExportQuery</c> via <c>vw_RegulatoryExport</c>.
/// </summary>
public sealed record RegulatoryExportRow(
    string? PreviousAppraisalNumber,
    string? LatestAppraisalNumber,
    string CollateralType,
    string? LatestAppraisalType,
    bool IsUnderConstruction,
    decimal? ConstructionProgressPercent,
    decimal? LatestAppraisalValue,
    decimal? EarliestAppraisalValue,
    int? NumberOfFloors,
    int? BuildingAge,
    DateTime? LatestAppraisalDate,
    DateTime? LatestProgressiveAppraisalDate,
    DateTime? EarliestAppraisalDate,
    Guid? LatestAppraisalCompanyId,
    string? DopaCode,
    decimal? LandAreaSqWa,
    decimal? BuildingArea,
    string? BuildingTypeCode,
    string? BuildingTypeDescription
);
