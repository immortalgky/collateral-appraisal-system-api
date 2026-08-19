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
/// Produced by <c>CollateralResultQuery</c>, formatted into a 208-char Detail record by
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
    /// <summary>
    /// Bank-staff valuer (CCDAPI/CCDAPN, positions 107-150). Populated only on the Internal path.
    /// </summary>
    string? InternalValuerCode,
    string? InternalValuerName,
    /// <summary>
    /// Appraisal-company valuer (CCDAPC/CCDAPE, positions 151-194). Populated only on the External
    /// path, off-system (Offline) engagements included.
    ///
    /// The two pairs are mutually exclusive: an appraisal ran on one path or the other, so exactly one
    /// pair carries data and the other goes out blank. <c>R</c> rows blank both.
    /// </summary>
    string? ExternalValuerCode,
    string? ExternalValuerName,
    int? LifeYear,
    string AppraisalStatus,
    /// <summary>
    /// Building age in years (CCEBIL, positions 199-201). Building types take the OLDEST building on
    /// the engagement; condo takes CondoDetails.BuildingAge. NULL for bare land and machinery.
    /// </summary>
    int? BuildingAge,
    /// <summary>
    /// Usable area in sq.m (CCEARE, positions 202-208, dec(7,2)). Building types take the SUM of every
    /// building on the engagement; condo takes CondoDetails.UsableArea. NULL for bare land, machinery,
    /// and any total that would overflow the field.
    /// </summary>
    decimal? AreaUtilization
);
