namespace Collateral.Contracts.FileInterface;

/// <summary>
/// Returns one outbound row per unsent appraisal:
/// - Status "A": completed appraisals not yet acknowledged for the collateral id they resolve to.
/// - Status "R": spooled rejected appraisals in PendingCollateralResults where SentAt is NULL.
/// </summary>
public interface ICollateralResultQuery
{
    Task<IReadOnlyList<CollateralResultRow>> GetUnsentRowsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// One row of the outbound Collateral Result interface.
///
/// Usually one row per completed appraisal. A block project is the exception: AS400 finances each
/// unit separately, so it gets one row per unit, each with its own price and land area.
///
/// Carries the typed field values plus the keys the export job needs for the sent-ledger. Formatted
/// into a 231-char Detail record by <c>CollateralResultFileWriter</c>.
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
    decimal? AreaUtilization,
    /// <summary>
    /// Whether AS400 may apply this result without a human looking at it (position 209).
    ///
    /// 'Y' only when the appraisal resolved to exactly one AS400 collateral. Where it resolved to
    /// several — or to none — the collateral id goes out blank and this goes out 'N', because we
    /// cannot say which collateral the price belongs to and guessing would update the wrong one.
    /// Every other field is still populated: they describe our appraisal and do not depend on the
    /// match succeeding.
    /// </summary>
    string AutoUpdate = "N",
    /// <summary>
    /// Land area of the collateral on THIS row, in Thai units (positions 210-221). A block project's
    /// row carries its own unit's area; any other appraisal carries the appraisal's total.
    /// </summary>
    int? LandAreaRai = null,
    int? LandAreaNgan = null,
    decimal? LandAreaSquareWa = null,
    /// <summary>
    /// Total land area of the whole appraisal in square wa (positions 222-231). Deliberately the same
    /// value on every row of a multi-row block project: it is the appraisal's total, not this row's
    /// share, and the two views of the area are what let the host reconcile them.
    /// </summary>
    decimal? LandAreaTotalSqWa = null
);
