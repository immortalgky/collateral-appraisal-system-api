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
                await IngestFileAsync(file, processedDirectory, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[REAPPRAISAL-AS400] Failed to ingest {File}", file.FileName);
            }
        }

        logger.LogInformation("[REAPPRAISAL-AS400] Ingestion run complete");
    }

    private async Task IngestFileAsync(
        InboundFileInfo file,
        string processedDirectory,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("[REAPPRAISAL-AS400] Processing {File}", file.FileName);

        var fileDate = CollatrevFileParser.ParseFilenameDate(file.FileName);
        if (fileDate is null)
        {
            logger.LogWarning("[REAPPRAISAL-AS400] Cannot parse date from filename '{File}', skipping", file.FileName);
            return;
        }

        await using var stream = await fileSource.OpenReadAsync(file, cancellationToken);
        var parsed = parser.ParseStream(stream);

        await ingestor.IngestAsync(file.FileName, fileDate.Value, parsed, cancellationToken);

        await fileSource.ArchiveAsync(file, processedDirectory, cancellationToken);

        logger.LogInformation("[REAPPRAISAL-AS400] Archived {File}", file.FileName);
    }
}
