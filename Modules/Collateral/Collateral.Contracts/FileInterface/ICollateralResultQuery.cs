namespace Collateral.Contracts.FileInterface;

/// <summary>
/// Returns one outbound row per unsent appraisal:
/// - Status "A": completed appraisals whose primary master has a HostCollateralId and are
///   absent from CollateralResultLogs (approved path, existing behaviour).
/// - Status "R": spooled rejected appraisals in PendingCollateralResults where SentAt is NULL
///   (rejected path, added to support AS400 R-record emission).
/// Reads the collateral schema only.
/// </summary>
public interface ICollateralResultQuery
{
    Task<IReadOnlyList<CollateralResultRow>> GetUnsentRowsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// One row of the outbound Collateral Result interface (per completed appraisal, primary master).
/// Carries the typed field values plus the keys the export job needs for the sent-ledger.
/// Produced by <c>CollateralResultQuery</c>, formatted into a 198-char Detail record by
/// <c>CollateralResultFileWriter</c>.
/// </summary>
public sealed record CollateralResultRow(
    Guid AppraisalId,
    string CollateralId,
    string AppraisalReportNumber,
    decimal? AppraisalValue,
    decimal? LandValue,
    decimal? BuildingValue,
    decimal? ForceSaleValue,
    DateOnly? CurrentAppraisalDate,
    DateOnly? NextAppraisalDate,
    string? InternalValuerCode,
    string? InternalValuerName,
    string? ExternalValuerCode,
    string? ExternalValuerName,
    int? LifeYear,
    string AppraisalStatus
);
