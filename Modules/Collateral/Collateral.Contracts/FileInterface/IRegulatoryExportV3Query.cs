namespace Collateral.Contracts.FileInterface;

/// <summary>
/// Version 3 of the regulatory snapshot: one row per COLLATERAL the bank holds, as AS400 reports it,
/// carrying the date and value of that collateral's FIRST appraisal.
///
/// v1 keys on CollateralMaster and v2 on the appraisal chain — both start from an appraisal and infer
/// the collateral. v3 stops inferring: the AS400 feed is already one row per collateral with the
/// appraisal number attached, so the row set is given, not derived. Returns the same
/// <see cref="RegulatoryExportRow"/>, so all three share the fixed-width and Excel writers unchanged.
///
/// A separate interface rather than a keyed registration, for the same reason v2 has one: the
/// versions run side by side during the changeover, each behind its own recurring job, and keyed DI
/// would let the wrong one resolve silently.
/// </summary>
public interface IRegulatoryExportV3Query
{
    Task<IReadOnlyList<RegulatoryExportRow>> GetRowsAsync(CancellationToken cancellationToken = default);
}
