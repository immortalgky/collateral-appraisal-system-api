namespace Collateral.Contracts.As400Legacy;

/// <summary>
/// Imports the AS400 legacy collateral listing (<c>appraisal.AS400ReportListing</c>) — collateral the
/// bank has held since before this system existed, valued in AS400 and never appraised in CAS.
///
/// Same split as <see cref="Collateral.Contracts.HostLink.IHostCollateralLinkIngestor"/>: the job in
/// Integration owns file transport and parsing, this implementation in Collateral owns the EF writes.
/// The job supplies the one thing the database cannot answer — which application numbers AS400 still
/// reports — because the link file is never persisted.
/// </summary>
public interface IAs400LegacyImporter
{
    /// <param name="stillReportedApplicationNumbers">
    /// Application numbers present in the latest AS400 link file. A listing row absent from this set
    /// is collateral AS400 has stopped reporting, i.e. the bank has released it; the importer creates
    /// nothing for those rather than inventing an asset.
    /// </param>
    Task<As400LegacyImportResult> ImportAsync(
        IReadOnlySet<string> stillReportedApplicationNumbers,
        CancellationToken cancellationToken = default);
}

/// <param name="Considered">Listing rows read.</param>
/// <param name="Attached">
/// Valuations added to a master we already knew — the collateral was re-appraised in CAS, so this is
/// its older history.
/// </param>
/// <param name="Created">Unidentified masters minted for collateral never appraised in CAS.</param>
/// <param name="SkippedNotHeld">Absent from the link file — AS400 no longer reports it.</param>
/// <param name="SkippedWouldBeLatest">
/// Would have become the master's newest engagement, which decides the outbound file and every
/// master-level screen. Left for a human rather than letting a 2013 valuation displace a 2025 one.
/// </param>
/// <param name="AlreadyPresent">Imported by an earlier run.</param>
public record As400LegacyImportResult(
    int Considered,
    int Attached,
    int Created,
    int SkippedNotHeld,
    int SkippedWouldBeLatest,
    int AlreadyPresent);
