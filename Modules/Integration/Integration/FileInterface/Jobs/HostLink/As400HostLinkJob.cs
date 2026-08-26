using Collateral.Contracts.HostLink;
using Hangfire;
using Integration.Contracts.FileInterface;
using Integration.Contracts.FileSource;
using Integration.Contracts.HostLink;
using Integration.FileInterface.Format.HostLink;
using Integration.Infrastructure.FileInterface;
using Microsoft.Extensions.Logging;

namespace Integration.FileInterface.Jobs.HostLink;

/// <summary>
/// Hangfire recurring job that ingests the AS400 COLLATLINK file — the feed that maps our appraisal
/// numbers to AS400 collateral ids (CCDCID).
///
/// <b>The file is monthly and is a full replace</b>, not a delta: whatever it contains is the entire
/// set of collateral the bank holds, and anything absent from it is no longer held. The job itself
/// still runs daily, because nothing guarantees the file lands at a particular hour on a particular
/// day — a monthly schedule that fires before delivery would miss the file for a whole month. With
/// the ledger in place a run that finds nothing new costs one directory listing.
///
/// Per run:
///   1. Resolve inbox directory + pattern from <c>integration.FileInterfaceConfigs</c>.
///   2. List files, then drop the ones the ledger already recorded as ingested (name + size).
///   3. Order what is left by the date in the file name, oldest first, so a backlog is applied in
///      the order it was produced rather than in whatever order the listing came back.
///   4. Download, hash, and claim through the ledger; parse; hand to the ingestor.
///   5. Archive only if the interface is configured with a processed directory.
///   6. Per-file try/catch so one bad file does not block the rest.
/// </summary>
public class As400HostLinkJob(
    IInboundFileSource fileSource,
    HostCollateralLinkFileParser parser,
    IHostCollateralLinkIngestor ingestor,
    IFileInterfaceConfigProvider configProvider,
    InboundFileLedger ledger,
    ILogger<As400HostLinkJob> logger)
{
    /// <summary>
    /// A run can outlive its schedule when the backlog is large or the host is slow. Two runs writing
    /// HostCollateralLinks at once would interleave their replaces, so the second one waits — and
    /// gives up rather than piling on if the first is still going after five minutes.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[HOST-LINK-AS400] Starting ingestion run");

        var cfg = await configProvider.GetAsync(FileInterfaceCodes.HostCollateralLink, cancellationToken);
        if (cfg is null || !cfg.IsActive)
        {
            logger.LogWarning("[HOST-LINK-AS400] No active config row for '{Code}'; skipping",
                FileInterfaceCodes.HostCollateralLink);
            return;
        }

        var directory = cfg.Directory ?? "./hostlink/inbox";
        var filePattern = cfg.FilePattern ?? "AS400_COLLATLINK_*.txt";

        // No processed directory configured means the drop folder is not ours to reorganise — the
        // production case. The ledger is what prevents reprocessing, so archiving is a convenience
        // for local runs only.
        var processedDirectory = cfg.ProcessedDirectory;
        var failedDirectory = processedDirectory is null
            ? null
            : $"{processedDirectory.TrimEnd('/')}/failed";

        var files = await fileSource.ListFilesAsync(directory, filePattern, cancellationToken);

        if (files.Count == 0)
        {
            logger.LogInformation("[HOST-LINK-AS400] No files found — nothing to do");
            return;
        }

        var pending = await ledger.FilterUnprocessedAsync(
            FileInterfaceCodes.HostCollateralLink, files, cancellationToken);

        if (pending.Count == 0)
        {
            logger.LogInformation("[HOST-LINK-AS400] All {Count} file(s) already ingested", files.Count);
            return;
        }

        // Oldest first. The file name is the only ordering signal we trust: RecordDate inside the
        // file is the transmission date and is identical on every row, and the listing order is
        // whatever the filesystem or SFTP server felt like returning.
        var ordered = pending
            .Select(f => (File: f, Date: HostCollateralLinkFileParser.ParseFilenameDate(f.FileName)))
            .OrderBy(x => x.Date ?? DateOnly.MaxValue)
            .ThenBy(x => x.File.FileName, StringComparer.Ordinal)
            .ToList();

        foreach (var (file, fileDate) in ordered)
        {
            try
            {
                await IngestFileAsync(file, fileDate, processedDirectory, failedDirectory, cancellationToken);
            }
            catch (Exception ex)
            {
                // Nothing is recorded as succeeded here, so the file stays eligible for the next run.
                logger.LogError(ex, "[HOST-LINK-AS400] Failed to ingest {File}; leaving for retry", file.FileName);
            }
        }

        logger.LogInformation("[HOST-LINK-AS400] Ingestion run complete");
    }

    private async Task IngestFileAsync(
        InboundFileInfo file,
        DateOnly? fileDate,
        string? processedDirectory,
        string? failedDirectory,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("[HOST-LINK-AS400] Processing {File}", file.FileName);

        var entry = await ledger.BeginAsync(
            FileInterfaceCodes.HostCollateralLink, file, fileDate, cancellationToken);

        if (fileDate is null)
        {
            // Without a date we cannot place the file in the replace sequence, so it can never be
            // applied safely.
            const string reason = "File name does not carry a parsable date.";
            logger.LogWarning("[HOST-LINK-AS400] {File}: {Reason} Quarantining", file.FileName, reason);

            await ledger.MarkQuarantinedAsync(entry, reason, cancellationToken);
            await QuarantineAsync(file, failedDirectory, cancellationToken);
            return;
        }

        byte[] content;
        string hash;
        await using (var stream = await fileSource.OpenReadAsync(file, cancellationToken))
        {
            (content, hash) = await InboundFileLedger.ReadAndHashAsync(stream, cancellationToken);
        }

        if (!await ledger.TryClaimAsync(entry, hash, cancellationToken))
        {
            await ArchiveAsync(file, processedDirectory, cancellationToken);
            return;
        }

        ParsedHostLinkFile parsed;
        try
        {
            using var buffer = new MemoryStream(content, writable: false);
            parsed = parser.ParseStream(buffer);
        }
        catch (FormatException ex)
        {
            // Bad layout will never parse, however many times it is retried.
            logger.LogError(ex, "[HOST-LINK-AS400] {File} has invalid format; quarantining", file.FileName);

            await ledger.MarkQuarantinedAsync(entry, ex.Message, cancellationToken);
            await QuarantineAsync(file, failedDirectory, cancellationToken);
            return;
        }

        var result = await ingestor.IngestAsync(file.FileName, fileDate.Value, parsed, cancellationToken);

        if (result.SkippedAsStale)
        {
            logger.LogWarning(
                "[HOST-LINK-AS400] {File} ({FileDate}) is older than the last applied file; not applied",
                file.FileName, fileDate);

            await ledger.MarkSkippedStaleAsync(
                entry, "A newer COLLATLINK file has already been applied.", cancellationToken);
            await ArchiveAsync(file, processedDirectory, cancellationToken);
            return;
        }

        logger.LogInformation(
            "[HOST-LINK-AS400] {File}: received={Received} updated={Updated} unchanged={Unchanged} deactivated={Deactivated}",
            file.FileName, result.Received, result.Updated, result.Unchanged, result.Deactivated);

        await ledger.MarkSucceededAsync(
            entry, result.Received, result.Updated, result.Unchanged, cancellationToken);

        await ArchiveAsync(file, processedDirectory, cancellationToken);
    }

    /// <summary>
    /// Moves the file out of the inbox when the interface is configured to do so. Best-effort by
    /// design: the ledger already guarantees the file will not be reprocessed, so a failed move is
    /// housekeeping noise rather than a correctness problem.
    /// </summary>
    private async Task ArchiveAsync(
        InboundFileInfo file, string? processedDirectory, CancellationToken cancellationToken)
    {
        if (processedDirectory is null)
            return;

        try
        {
            await fileSource.ArchiveAsync(file, processedDirectory, cancellationToken);
            logger.LogInformation("[HOST-LINK-AS400] Archived {File}", file.FileName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[HOST-LINK-AS400] Could not archive {File}; ledger still prevents reprocessing",
                file.FileName);
        }
    }

    private async Task QuarantineAsync(
        InboundFileInfo file, string? failedDirectory, CancellationToken cancellationToken)
    {
        if (failedDirectory is null)
            return;

        try
        {
            await fileSource.ArchiveAsync(file, failedDirectory, cancellationToken);
            logger.LogWarning("[HOST-LINK-AS400] Quarantined {File} → {Dir}", file.FileName, failedDirectory);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[HOST-LINK-AS400] Could not move {File} to the failed folder", file.FileName);
        }
    }
}
