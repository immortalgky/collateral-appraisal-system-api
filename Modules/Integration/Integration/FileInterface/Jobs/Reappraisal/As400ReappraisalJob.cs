using Collateral.Contracts.Reappraisal;
using Hangfire;
using Integration.Contracts.FileInterface;
using Integration.Contracts.FileSource;
using Integration.Contracts.Reappraisal;
using Integration.FileInterface.Format.Reappraisal;
using Integration.Infrastructure.FileInterface;
using Microsoft.Extensions.Logging;

namespace Integration.FileInterface.Jobs.Reappraisal;

/// <summary>
/// Hangfire recurring job that ingests the AS400 COLLATREV file — the list of appraisals the bank
/// wants reviewed.
///
/// <b>The file is monthly but the job runs daily.</b> Nothing guarantees it lands at a given hour on
/// a given day, and a monthly schedule that fires before delivery would leave the file sitting in the
/// inbox for another month. A run that finds nothing new costs one directory listing.
///
/// Per run:
///   1. Resolve inbox directory + pattern from <c>integration.FileInterfaceConfigs</c>.
///   2. List files, then drop the ones the ledger already recorded as ingested (name + size).
///   3. Order what is left by the date in the file name, oldest first.
///   4. Download, hash, and claim through the ledger; parse; hand to the ingestor.
///   5. Archive only if the interface is configured with a processed directory.
///   6. Per-file try/catch so one bad file does not block the rest.
/// </summary>
public class As400ReappraisalJob(
    IInboundFileSource fileSource,
    CollatrevFileParser parser,
    IReappraisalIngestor ingestor,
    IFileInterfaceConfigProvider configProvider,
    InboundFileLedger ledger,
    ILogger<As400ReappraisalJob> logger)
{
    /// <summary>
    /// Candidates are keyed by (file date, collateral id, survey number); two runs importing the same
    /// file at once would race on that unique index. The second run waits, then gives up rather than
    /// piling on.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[REAPPRAISAL-AS400] Starting ingestion run");

        var cfg = await configProvider.GetAsync(FileInterfaceCodes.Reappraisal, cancellationToken);
        if (cfg is null || !cfg.IsActive)
        {
            logger.LogWarning("[REAPPRAISAL-AS400] No active config row for '{Code}'; skipping",
                FileInterfaceCodes.Reappraisal);
            return;
        }

        var directory = cfg.Directory ?? "./reappraisal/inbox";
        var filePattern = cfg.FilePattern ?? "AS400_COLLATREV_*.txt";

        // Null means the drop folder is not ours to reorganise — the production case. The ledger is
        // what prevents reprocessing; archiving is a local-run convenience.
        var processedDirectory = cfg.ProcessedDirectory;
        var failedDirectory = processedDirectory is null
            ? null
            : $"{processedDirectory.TrimEnd('/')}/failed";

        var files = await fileSource.ListFilesAsync(directory, filePattern, cancellationToken);

        if (files.Count == 0)
        {
            logger.LogInformation("[REAPPRAISAL-AS400] No files found — nothing to do");
            return;
        }

        var pending = await ledger.FilterUnprocessedAsync(
            FileInterfaceCodes.Reappraisal, files, cancellationToken);

        if (pending.Count == 0)
        {
            logger.LogInformation("[REAPPRAISAL-AS400] All {Count} file(s) already ingested", files.Count);
            return;
        }

        var ordered = pending
            .Select(f => (File: f, Date: CollatrevFileParser.ParseFilenameDate(f.FileName)))
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
                // Nothing recorded as succeeded, so the file stays eligible for the next run.
                logger.LogError(ex, "[REAPPRAISAL-AS400] Failed to ingest {File}; leaving for retry", file.FileName);
            }
        }

        logger.LogInformation("[REAPPRAISAL-AS400] Ingestion run complete");
    }

    private async Task IngestFileAsync(
        InboundFileInfo file,
        DateOnly? fileDate,
        string? processedDirectory,
        string? failedDirectory,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("[REAPPRAISAL-AS400] Processing {File}", file.FileName);

        var entry = await ledger.BeginAsync(
            FileInterfaceCodes.Reappraisal, file, fileDate, cancellationToken);

        if (fileDate is null)
        {
            // Candidates are de-duplicated within a source file date; without one there is no scope
            // to import into.
            const string reason = "File name does not carry a parsable date.";
            logger.LogWarning("[REAPPRAISAL-AS400] {File}: {Reason} Quarantining", file.FileName, reason);

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

        ParsedReappraisalFile parsed;
        try
        {
            using var buffer = new MemoryStream(content, writable: false);
            parsed = parser.ParseStream(buffer);
        }
        catch (FormatException ex)
        {
            logger.LogError(ex, "[REAPPRAISAL-AS400] {File} has invalid format; quarantining", file.FileName);

            await ledger.MarkQuarantinedAsync(entry, ex.Message, cancellationToken);
            await QuarantineAsync(file, failedDirectory, cancellationToken);
            return;
        }

        await ingestor.IngestAsync(file.FileName, fileDate.Value, parsed, cancellationToken);

        await ledger.MarkSucceededAsync(entry, parsed.Details.Count, 0, 0, cancellationToken);

        await ArchiveAsync(file, processedDirectory, cancellationToken);
    }

    /// <summary>
    /// Best-effort: the ledger already guarantees the file will not be reprocessed, so a failed move
    /// is housekeeping noise rather than a correctness problem.
    /// </summary>
    private async Task ArchiveAsync(
        InboundFileInfo file, string? processedDirectory, CancellationToken cancellationToken)
    {
        if (processedDirectory is null)
            return;

        try
        {
            await fileSource.ArchiveAsync(file, processedDirectory, cancellationToken);
            logger.LogInformation("[REAPPRAISAL-AS400] Archived {File}", file.FileName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[REAPPRAISAL-AS400] Could not archive {File}; ledger still prevents reprocessing",
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
            logger.LogWarning("[REAPPRAISAL-AS400] Quarantined {File} → {Dir}", file.FileName, failedDirectory);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[REAPPRAISAL-AS400] Could not move {File} to the failed folder", file.FileName);
        }
    }
}
