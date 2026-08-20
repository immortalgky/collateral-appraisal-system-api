using System.Collections.Concurrent;
using Collateral.Contracts.As400Legacy;
using Integration.Contracts.FileInterface;
using Integration.Contracts.FileSource;
using Integration.FileInterface.Format.HostLink;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Integration.FileInterface.Jobs.HostLink;

/// <summary>
/// One-shot import of the AS400 legacy collateral listing. Admin-triggered, not scheduled.
///
/// Same split as <see cref="As400HostLinkJob"/>: this job owns file transport and parsing, and
/// <see cref="IAs400LegacyImporter"/> in Collateral owns the writes. It exists at all because of one
/// question the database cannot answer — <b>which of the legacy collateral does AS400 still report?</b>
/// The link file is parsed and archived, never persisted, so the set of live application numbers has
/// to be read from the file at import time. A listing row absent from it is collateral the bank has
/// released, and the importer must not mint a master for it.
///
/// <b>Where the file is looked for.</b> Inbox first, then the processed folder. The import is
/// normally run after the nightly link ingest has already archived the file, but running it before
/// is equally valid, so both locations are searched rather than forcing an operator to move files
/// around. The newest matching file wins.
/// </summary>
/// <remarks>
/// Singleton, so that a fire-and-forget run outlives the request that started it and the caller can
/// poll for the outcome. Everything it needs is scoped, so it takes only the scope factory and opens
/// its own scope per run — the same shape as <c>CollateralBackfillJob</c>.
/// </remarks>
public class As400LegacyImportJob(
    IServiceScopeFactory scopeFactory,
    ILogger<As400LegacyImportJob> logger)
{
    private readonly ConcurrentDictionary<Guid, As400LegacyImportStatus> _jobs = new();

    /// <summary>Fire-and-forget, mirroring CollateralBackfillJob: the caller gets an id immediately.</summary>
    public Guid Start()
    {
        var jobId = Guid.CreateVersion7();
        _jobs[jobId] = new As400LegacyImportStatus(jobId, "InProgress", null, null);

        _ = Task.Run(async () =>
        {
            try
            {
                var result = await ExecuteAsync(CancellationToken.None);
                _jobs[jobId] = new As400LegacyImportStatus(jobId, "Completed", result, null);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[AS400-LEGACY-IMPORT] Job {JobId} failed", jobId);
                _jobs[jobId] = new As400LegacyImportStatus(jobId, "Failed", null, ex.Message);
            }
        });

        return jobId;
    }

    public As400LegacyImportStatus? GetStatus(Guid jobId)
        => _jobs.TryGetValue(jobId, out var s) ? s : null;

    public async Task<As400LegacyImportResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[AS400-LEGACY-IMPORT] Starting");

        using var scope = scopeFactory.CreateScope();
        var configProvider = scope.ServiceProvider.GetRequiredService<IFileInterfaceConfigProvider>();
        var fileSource = scope.ServiceProvider.GetRequiredService<IInboundFileSource>();
        var parser = scope.ServiceProvider.GetRequiredService<HostCollateralLinkFileParser>();

        var cfg = await configProvider.GetAsync(FileInterfaceCodes.HostCollateralLink, cancellationToken);
        if (cfg is null || !cfg.IsActive)
            throw new InvalidOperationException(
                $"No active file-interface config for '{FileInterfaceCodes.HostCollateralLink}'; "
                + "the legacy import cannot tell which collateral AS400 still reports.");

        var filePattern = cfg.FilePattern ?? "AS400_COLLATLINK_*.txt";
        var stillReported = await ReadReportedNumbersAsync(
            fileSource, parser,
            [cfg.Directory ?? "./hostlink/inbox", cfg.ProcessedDirectory ?? "./hostlink/processed"],
            filePattern, cancellationToken);

        // Refusing here is deliberate. With an empty set every listing row falls to "not reported",
        // so the import would silently do nothing and look like a success — the failure mode is
        // indistinguishable from "there was nothing to do".
        if (stillReported.Count == 0)
            throw new InvalidOperationException(
                $"No AS400 link file matching '{filePattern}' was found in the inbox or the processed "
                + "folder. The import needs it to tell which legacy collateral the bank still holds.");

        var importer = scope.ServiceProvider.GetRequiredService<IAs400LegacyImporter>();
        return await importer.ImportAsync(stillReported, cancellationToken);
    }

    /// <summary>Application numbers from the newest link file found across the given directories.</summary>
    private async Task<HashSet<string>> ReadReportedNumbersAsync(
        IInboundFileSource fileSource, HostCollateralLinkFileParser parser,
        string[] directories, string filePattern, CancellationToken cancellationToken)
    {
        foreach (var directory in directories.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var files = await fileSource.ListFilesAsync(directory, filePattern, cancellationToken);
            if (files.Count == 0) continue;

            // ListFilesAsync gives no ordering guarantee; the filename carries the feed date, so the
            // last one by name is the most recent snapshot of what AS400 holds.
            var file = files.OrderByDescending(f => f.FileName, StringComparer.Ordinal).First();
            logger.LogInformation(
                "[AS400-LEGACY-IMPORT] Reading still-reported collateral from {File} in {Directory}",
                file.FileName, directory);

            await using var stream = await fileSource.OpenReadAsync(file, cancellationToken);
            var parsed = parser.ParseStream(stream);

            var numbers = parsed.Records
                .Select(r => r.AppraisalReportNumber)
                .ToHashSet(StringComparer.Ordinal);

            logger.LogInformation(
                "[AS400-LEGACY-IMPORT] {File} reports {Count} distinct application number(s)",
                file.FileName, numbers.Count);

            return numbers;
        }

        return [];
    }
}

public record As400LegacyImportStatus(
    Guid JobId, string Status, As400LegacyImportResult? Result, string? Error);
