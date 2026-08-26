namespace Collateral.Contracts.FileInterface;

/// <summary>
/// Returns one row per COLLATERAL the bank holds, as the AS400 feed reports it, carrying the date and
/// value of that collateral's FIRST appraisal.
///
/// The row set is given, not derived: <c>collateral.HostCollateralLinks</c> is already one row per
/// AS400 collateral id with the appraisal number attached, so nothing has to infer which collateral an
/// appraisal stands for. PrevAppraisalId is still walked, but only to reach the oldest ancestor.
///
/// No sent-ledger: every run is a full re-extract.
/// </summary>
public interface IRegulatoryExportQuery
{
    Task<IReadOnlyList<RegulatoryExportRow>> GetRowsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// One row of the outbound CAS-AS400-Regulatory interface — one per collateral the bank holds.
/// Carries typed field values that <c>RegulatoryFileWriter</c> formats into a 300-char Detail record
/// and <c>RegulatoryExcelWriter</c> renders into the companion workbook.
/// Produced by <c>RegulatoryExportQuery</c> via <c>vw_RegulatoryExport</c>.
/// </summary>
public sealed record RegulatoryExportRow(
    string? LatestAppraisalNumber,
    string CollateralType,
    string? HostCollateralId,
    string? LatestAppraisalType,
    bool IsUnderConstruction,
    decimal? ConstructionProgressPercent,
    decimal? LatestAppraisalValue,
    decimal? EarliestAppraisalValue,
    // Value with part-built buildings counted at their construction progress rather than at 100%,
    // computed for the appraisal AS400 named by the same rule as the Appraisal module's
    // IConstructionCurrentValueService. NULL when that appraisal had no construction inspection —
    // nothing was part-built, so the writer falls back to LatestAppraisalValue.
    decimal? CurrentValue,
    decimal? SellingPrice,
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
