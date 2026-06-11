using Integration.Contracts.Reappraisal;

namespace Collateral.Contracts.Reappraisal;

/// <summary>
/// Upserts reappraisal candidates parsed from a COLLATREV file into the Collateral data store.
/// The implementation (in Collateral) owns the EF/Dapper writes; the job (in Integration)
/// owns only file transport and parsing.
/// </summary>
public interface IReappraisalIngestor
{
    Task IngestAsync(
        string fileName,
        DateOnly fileDate,
        ParsedReappraisalFile parsed,
        CancellationToken cancellationToken = default);
}
