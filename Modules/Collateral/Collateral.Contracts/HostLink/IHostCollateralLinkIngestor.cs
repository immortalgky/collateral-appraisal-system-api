using Integration.Contracts.HostLink;

namespace Collateral.Contracts.HostLink;

/// <summary>
/// Ingests the AS400 HOST_COLLATERAL_LINK feed into the Collateral data store.
/// The implementation (in Collateral) owns the EF writes; the job (in Integration) owns only file
/// transport and parsing — the same split as <c>IReappraisalIngestor</c>.
/// </summary>
public interface IHostCollateralLinkIngestor
{
    /// <summary>
    /// <b>The feed is a full monthly replace, not a delta.</b> Every row it carries is upserted and
    /// stamped with the file's date; rows it omits keep an older date and thereby leave the active
    /// set. Nothing is deleted, and a file older than the last one applied is refused outright.
    ///
    /// Records the feed in <c>collateral.HostCollateralLinks</c> keyed by collateral id, then
    /// resolves each number through its <c>CollateralEngagement</c> to a <c>CollateralMaster</c> and
    /// writes the AS400 id and pledge state onto the master.
    ///
    /// The two steps are deliberately independent: the link table is written first so an id still
    /// lands for the thousands of completed appraisals that never got a master, which would otherwise
    /// be reported as <see cref="HostLinkIngestResult.NotFound"/> and dropped.
    ///
    /// The id belongs to the master rather than the engagement because it names the collateral, not
    /// one appraisal of it. Aliases (the other titles of the same physical group) receive the
    /// redemption flags only — one AS400 id per group.
    ///
    /// Block projects are the exception: AS400 issues one id per financed unit, which is the grain of
    /// <c>collateral.ProjectUnits</c>, not of the project's single engagement. Those rows are reported
    /// and left unwritten (see <see cref="HostLinkIngestResult.ProjectSkipped"/>).
    /// </summary>
    Task<HostLinkIngestResult> IngestAsync(
        string fileName,
        DateOnly fileDate,
        ParsedHostLinkFile parsed,
        CancellationToken cancellationToken = default);
}

/// <param name="SkippedAsStale">
/// True when the file was rejected before anything was written because a newer COLLATLINK file has
/// already been applied. The feed is a full replace, so applying an older file would resurrect
/// collateral the bank has since released and undo the current month's state. Every other counter is
/// 0 when this is set.
/// </param>
/// <param name="Deactivated">
/// Rows the incoming file no longer mentions. They keep their previous <c>LastSeenFileDate</c>, which
/// puts them outside the active set without deleting anything — recoverable if a partial file ever
/// arrives, and it stays visible which round a collateral dropped out.
/// </param>
/// <param name="Received">Rows read from the file, after collapsing in-file duplicates.</param>
/// <param name="Updated">Masters whose id or pledge state changed.</param>
/// <param name="Unchanged">Masters skipped because they already held these values.</param>
/// <param name="NotFound">
/// Appraisal numbers that reached no master — either no engagement exists for them, or the engagement
/// resolved to a master that has since been deleted. Skipped with a warning; the id is still recorded
/// in <c>collateral.HostCollateralLinks</c>. Expected in bulk for collateral appraised before this
/// system existed, which a full AS400 dump also carries; otherwise it means
/// <c>AppraisalCompletedConsumer</c> dead-lettered.
/// </param>
/// <param name="ProjectSkipped">
/// Block-project appraisals whose ids were logged but not written. AS400 issues one id per financed
/// unit while the master holds a single id, so writing one would let a single unit decide the whole
/// project's pledge state. A unit's id belongs on <c>collateral.ProjectUnits.HostCollateralId</c>;
/// per-unit ingest is not implemented yet.
///
/// Expect this to be 0 in practice: AS400 packs a 2-digit unit sequence onto the appraisal number, so
/// block rows fail the engagement lookup and are counted under <paramref name="NotFound"/> instead.
/// This counter covers a project appraisal number arriving with no sequence.
/// </param>
public record HostLinkIngestResult(
    int Received,
    int Updated,
    int Unchanged,
    int NotFound,
    int ProjectSkipped,
    int Deactivated = 0,
    bool SkippedAsStale = false)
{
    /// <summary>The file was older than the one already applied; nothing was written.</summary>
    public static HostLinkIngestResult Stale() => new(0, 0, 0, 0, 0, 0, true);
}
