using Collateral.Contracts.HostLink;
using Integration.Contracts.FileInterface;
using Integration.Contracts.FileSource;
using Integration.FileInterface.Format.HostLink;
using Microsoft.Extensions.Logging;

namespace Integration.FileInterface.Jobs.HostLink;

/// <summary>
/// Hangfire recurring job that ingests the nightly AS400 COLLATLINK file — the feed that maps our
/// appraisal numbers to AS400 collateral ids (CCDCID).
///
/// <b>Runs before collateral-result-export (which is at 00:00)</b>, so ids landed tonight are
/// echoed on tonight's outbound result file rather than a day late.
///
/// Algorithm per file:
///   1. Resolve inbox directory + pattern from <c>integration.FileInterfaceConfigs</c>.
///   2. List files from IInboundFileSource.
///   3. Download + parse (UTF-8 fixed-width, 39-char Detail records; H/D/T).
///   4. Delegate the upsert + master resolution to <see cref="IHostCollateralLinkIngestor"/>.
///   5. Archive file via IInboundFileSource.ArchiveAsync.
///   6. Per-file try/catch so one bad file does not block others.
/// </summary>
public class As400HostLinkJob(
    IInboundFileSource fileSource,
    HostCollateralLinkFileParser parser,
    IHostCollateralLinkIngestor ingestor,
    IFileInterfaceConfigProvider configProvider,
    ILogger<As400HostLinkJob> logger)
{
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
        var processedDirectory = cfg.ProcessedDirectory ?? "./hostlink/processed";
        // Files that can never succeed (bad filename / invalid format) are moved here so they leave
        // the inbox and are not re-listed and re-failed on every run.
        var failedDirectory = $"{processedDirectory.TrimEnd('/')}/failed";

        var files = await fileSource.ListFilesAsync(directory, filePattern, cancellationToken);

        if (files.Count == 0)
        {
            logger.LogInformation("[HOST-LINK-AS400] No files found — nothing to do");
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
                logger.LogError(ex, "[HOST-LINK-AS400] {File} has invalid format; quarantining", file.FileName);
                await QuarantineAsync(file, failedDirectory, cancellationToken);
            }
            catch (Exception ex)
            {
                // Likely transient (DB/network) — leave the file in place so the next run retries it.
                logger.LogError(ex, "[HOST-LINK-AS400] Failed to ingest {File}; leaving for retry", file.FileName);
            }
        }

        logger.LogInformation("[HOST-LINK-AS400] Ingestion run complete");
    }

    private async Task IngestFileAsync(
        InboundFileInfo file,
        string processedDirectory,
        string failedDirectory,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("[HOST-LINK-AS400] Processing {File}", file.FileName);

        var fileDate = HostCollateralLinkFileParser.ParseFilenameDate(file.FileName);
        if (fileDate is null)
        {
            logger.LogWarning("[HOST-LINK-AS400] Cannot parse date from filename '{File}'; quarantining",
                file.FileName);
            await QuarantineAsync(file, failedDirectory, cancellationToken);
            return;
        }

        await using var stream = await fileSource.OpenReadAsync(file, cancellationToken);
        var parsed = parser.ParseStream(stream);

        var result = await ingestor.IngestAsync(file.FileName, fileDate.Value, parsed, cancellationToken);

        logger.LogInformation(
            "[HOST-LINK-AS400] {File}: received={Received} updated={Updated} "
            + "unchanged={Unchanged} notFound={NotFound} projectSkipped={ProjectSkipped}",
            file.FileName, result.Received, result.Updated, result.Unchanged, result.NotFound,
            result.ProjectSkipped);

        await fileSource.ArchiveAsync(file, processedDirectory, cancellationToken);

        logger.LogInformation("[HOST-LINK-AS400] Archived {File}", file.FileName);
    }

    /// <summary>
    /// Moves a permanently-unprocessable file out of the inbox into the failed directory.
    /// Swallows move errors (logs them) so one un-movable file cannot break the run.
    /// </summary>
    private async Task QuarantineAsync(
        InboundFileInfo file,
        string failedDirectory,
        CancellationToken cancellationToken)
    {
        try
        {
            await fileSource.ArchiveAsync(file, failedDirectory, cancellationToken);
            logger.LogWarning("[HOST-LINK-AS400] Quarantined {File} → {Dir}", file.FileName, failedDirectory);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[HOST-LINK-AS400] Could not quarantine {File}; it may be reprocessed",
                file.FileName);
        }
    }
}
