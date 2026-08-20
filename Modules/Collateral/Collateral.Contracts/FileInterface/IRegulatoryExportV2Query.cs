namespace Collateral.Contracts.FileInterface;

/// <summary>
/// Version 2 of the regulatory snapshot: one row per APPRAISAL CHAIN, plus one per AS400 legacy
/// listing row no chain has taken over. Returns the same <see cref="RegulatoryExportRow"/> as v1, so
/// both versions share the fixed-width and Excel writers unchanged.
///
/// A separate interface rather than a second registration of <see cref="IRegulatoryExportQuery"/>:
/// the two run side by side during the changeover — each behind its own recurring job — and keyed DI
/// would make it possible to resolve the wrong one silently. Two names cannot be confused.
/// </summary>
public interface IRegulatoryExportV2Query
{
    Task<IReadOnlyList<RegulatoryExportRow>> GetRowsAsync(CancellationToken cancellationToken = default);
}
