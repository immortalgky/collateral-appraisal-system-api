using Collateral.Contracts.Reappraisal;
using Integration.Contracts.FileInterface;
using Integration.Contracts.FileSource;
using Integration.FileInterface.Format.Reappraisal;
using Microsoft.Extensions.Logging;

namespace Integration.FileInterface.Jobs.Reappraisal;

/// <summary>
/// Hangfire recurring job that ingests the monthly AS400 COLLATREV file.
///
/// Algorithm per file:
///   1. Resolve inbox directory + pattern from <c>integration.FileInterfaceConfigs</c>.
///   2. List files from IInboundFileSource.
///   3. Download + parse (UTF-8 fixed-width, 649-char Detail records; H/D/T).
///   4. Delegate the upsert + lat/lon enrichment to <see cref="IReappraisalIngestor"/>.
///   5. Archive file via IInboundFileSource.ArchiveAsync.
///   6. Per-file try/catch so one bad file does not block others.
/// </summary>
public class As400ReappraisalJob(
    IInboundFileSource fileSource,
    CollatrevFileParser parser,
    IReappraisalIngestor ingestor,
    IFileInterfaceConfigProvider configProvider,
    ILogger<As400ReappraisalJob> logger)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[REAPPRAISAL-AS400] Starting ingestion run");

        var cfg = await configProvider.GetAsync(FileInterfaceCodes.Reappraisal, cancellationToken);
        if (cfg is null || !cfg.IsActive)
        {
            logger.LogWarning("[REAPPRAISAL-AS400] No active config row for '{Code}'; skipping", FileInterfaceCodes.Reappraisal);
            return;
        }

        var directory = cfg.Directory ?? "./reappraisal/inbox";
        var filePattern = cfg.FilePattern ?? "AS400_COLLATREV_*.txt";
        var processedDirectory = cfg.ProcessedDirectory ?? "./reappraisal/processed";
        // Files that can never succeed (bad filename / invalid format) are moved here so they leave the
        // inbox and are not re-listed and re-failed on every run.
        var failedDirectory = $"{processedDirectory.TrimEnd('/')}/failed";

        var files = await fileSource.ListFilesAsync(directory, filePattern, cancellationToken);

        if (files.Count == 0)
        {
            logger.LogInformation("[REAPPRAISAL-AS400] No files found — nothing to do");
            return;
        }

        foreach (var file in files)
        {
            try
            {
                await IngestFileAsync(file, processedDirectory, failedDirectory, cancellationToken);
            }
            catch (FormatException ex)
            {
                // Bad data the file will never parse — quarantine so it is not reprocessed forever.
                logger.LogError(ex, "[REAPPRAISAL-AS400] {File} has invalid format; quarantining", file.FileName);
                await QuarantineAsync(file, failedDirectory, cancellationToken);
            }
            catch (Exception ex)
            {
                // Likely transient (DB/network) — leave the file in place so the next run retries it.
                logger.LogError(ex, "[REAPPRAISAL-AS400] Failed to ingest {File}; leaving for retry", file.FileName);
            }
        }

        logger.LogInformation("[REAPPRAISAL-AS400] Ingestion run complete");
    }

    private async Task IngestFileAsync(
        InboundFileInfo file,
        string processedDirectory,
        string failedDirectory,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("[REAPPRAISAL-AS400] Processing {File}", file.FileName);

        var fileDate = CollatrevFileParser.ParseFilenameDate(file.FileName);
        if (fileDate is null)
        {
            logger.LogWarning("[REAPPRAISAL-AS400] Cannot parse date from filename '{File}'; quarantining", file.FileName);
            await QuarantineAsync(file, failedDirectory, cancellationToken);
            return;
        }

        await using var stream = await fileSource.OpenReadAsync(file, cancellationToken);
        var parsed = parser.ParseStream(stream);

        await ingestor.IngestAsync(file.FileName, fileDate.Value, parsed, cancellationToken);

        await fileSource.ArchiveAsync(file, processedDirectory, cancellationToken);

        logger.LogInformation("[REAPPRAISAL-AS400] Archived {File}", file.FileName);
    }

    /// <summary>
    /// Moves a permanently-unprocessable file out of the inbox into the failed directory.
    /// Swallows move errors (logs them) so one un-movable file cannot break the run.
    /// </summary>
    private async Task QuarantineAsync(InboundFileInfo file, string failedDirectory, CancellationToken cancellationToken)
    {
        try
        {
            await fileSource.ArchiveAsync(file, failedDirectory, cancellationToken);
            logger.LogWarning("[REAPPRAISAL-AS400] Quarantined {File} → {Dir}", file.FileName, failedDirectory);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[REAPPRAISAL-AS400] Could not quarantine {File}; it may be reprocessed", file.FileName);
        }
    }
}
