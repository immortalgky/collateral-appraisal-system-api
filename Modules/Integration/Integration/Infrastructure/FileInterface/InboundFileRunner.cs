using Integration.Contracts.FileInterface;
using Integration.Contracts.FileSource;
using Microsoft.Extensions.Logging;

namespace Integration.Infrastructure.FileInterface;

/// <summary>
/// What one inbound AS400 interface has to say about itself for <see cref="InboundFileRunner"/> to
/// run it. Everything else — listing, de-duplication, ordering, hashing, the ledger, archiving, the
/// per-file error boundary — is the same for every interface and lives in the runner.
/// </summary>
/// <param name="ParseFileDate">
/// Reads the date out of the file name. It is the only ordering signal we trust: the date fields
/// INSIDE these files are the transmission date and are identical on every row, and the listing
/// order is whatever the filesystem or SFTP server felt like returning. A file whose name carries no
/// date is quarantined rather than guessed at.
/// </param>
/// <param name="IngestAsync">
/// Parses the bytes and applies them. Throwing <see cref="FormatException"/> means the layout is
/// wrong and will never parse, so the runner quarantines instead of retrying; any other exception is
/// treated as transient and leaves the file eligible for the next run.
/// </param>
public sealed record InboundFileInterface(
    string Code,
    string LogTag,
    string DefaultDirectory,
    string DefaultFilePattern,
    Func<string, DateOnly?> ParseFileDate,
    Func<string, byte[], DateOnly, CancellationToken, Task<InboundIngestOutcome>> IngestAsync);

/// <param name="SkippedStale">
/// The file is older than what has already been applied. Recorded and archived, never applied — a
/// full-replace feed arriving out of order would otherwise reinstate collateral the bank has let go.
/// </param>
public sealed record InboundIngestOutcome(
    int Received,
    int Updated = 0,
    int Unchanged = 0,
    bool SkippedStale = false,
    string? StaleReason = null,
    string? Summary = null);

/// <summary>
/// Runs one inbound file interface end to end.
///
/// Both AS400 inbound feeds are monthly, and both are ingested by a job that runs DAILY. Nothing
/// guarantees a file lands at a given hour on a given day, and a monthly schedule that fires before
/// delivery would leave the file sitting in the inbox for another month. With the ledger in place a
/// run that finds nothing new costs one directory listing.
///
/// Per run:
///   1. Resolve inbox directory + pattern from <c>integration.FileInterfaceConfigs</c>.
///   2. List files, then drop the ones the ledger already recorded as ingested — on name and size,
///      so nothing is downloaded to find out it is old. Production drop folders are never emptied,
///      so this listing only grows.
///   3. Order what is left by the date in the file name, oldest first, so a backlog is applied in
///      the order it was produced.
///   4. Download, hash, and claim through the ledger — the second, exact pass that catches a file
///      reissued under the same name and size; parse; apply.
///   5. Archive, but only if the interface is configured with a processed directory. Production
///      folders belong to AS400 and cannot be reorganised; the ledger is what prevents reprocessing,
///      so moving files is a local-run convenience.
///   6. Per-file try/catch, so one bad file does not block the rest of the backlog.
/// </summary>
public class InboundFileRunner(
    IInboundFileSource fileSource,
    IFileInterfaceConfigProvider configProvider,
    InboundFileLedger ledger,
    ILogger<InboundFileRunner> logger)
{
    public async Task RunAsync(InboundFileInterface iface, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("{Tag} Starting ingestion run", iface.LogTag);

        var cfg = await configProvider.GetAsync(iface.Code, cancellationToken);
        if (cfg is null || !cfg.IsActive)
        {
            logger.LogWarning("{Tag} No active config row for '{Code}'; skipping", iface.LogTag, iface.Code);
            return;
        }

        var directory = cfg.Directory ?? iface.DefaultDirectory;
        var filePattern = cfg.FilePattern ?? iface.DefaultFilePattern;

        var processedDirectory = cfg.ProcessedDirectory;
        var failedDirectory = processedDirectory is null
            ? null
            : $"{processedDirectory.TrimEnd('/')}/failed";

        var files = await fileSource.ListFilesAsync(directory, filePattern, cancellationToken);
        if (files.Count == 0)
        {
            logger.LogInformation("{Tag} No files found — nothing to do", iface.LogTag);
            return;
        }

        var pending = await ledger.FilterUnprocessedAsync(iface.Code, files, cancellationToken);
        if (pending.Count == 0)
        {
            logger.LogInformation("{Tag} All {Count} file(s) already ingested", iface.LogTag, files.Count);
            return;
        }

        var ordered = pending
            .Select(f => (File: f, Date: iface.ParseFileDate(f.FileName)))
            .OrderBy(x => x.Date ?? DateOnly.MaxValue)
            .ThenBy(x => x.File.FileName, StringComparer.Ordinal)
            .ToList();

        foreach (var (file, fileDate) in ordered)
        {
            try
            {
                await IngestFileAsync(iface, file, fileDate, processedDirectory, failedDirectory, cancellationToken);
            }
            catch (Exception ex)
            {
                // Nothing is recorded as succeeded here, so the file stays eligible for the next run.
                logger.LogError(ex, "{Tag} Failed to ingest {File}; leaving for retry", iface.LogTag, file.FileName);
            }
        }

        logger.LogInformation("{Tag} Ingestion run complete", iface.LogTag);
    }

    private async Task IngestFileAsync(
        InboundFileInterface iface,
        InboundFileInfo file,
        DateOnly? fileDate,
        string? processedDirectory,
        string? failedDirectory,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("{Tag} Processing {File}", iface.LogTag, file.FileName);

        var entry = await ledger.BeginAsync(iface.Code, file, fileDate, cancellationToken);

        if (fileDate is null)
        {
            // Without a date the file cannot be placed in the sequence, so it can never be applied
            // safely however many times it is retried.
            const string reason = "File name does not carry a parsable date.";
            logger.LogWarning("{Tag} {File}: {Reason} Quarantining", iface.LogTag, file.FileName, reason);

            await ledger.MarkQuarantinedAsync(entry, reason, cancellationToken);
            await MoveAsync(iface, file, failedDirectory, "Quarantined", cancellationToken);
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
            await MoveAsync(iface, file, processedDirectory, "Archived", cancellationToken);
            return;
        }

        InboundIngestOutcome outcome;
        try
        {
            outcome = await iface.IngestAsync(file.FileName, content, fileDate.Value, cancellationToken);
        }
        catch (FormatException ex)
        {
            // Bad layout will never parse, however many times it is retried.
            logger.LogError(ex, "{Tag} {File} has invalid format; quarantining", iface.LogTag, file.FileName);

            await ledger.MarkQuarantinedAsync(entry, ex.Message, cancellationToken);
            await MoveAsync(iface, file, failedDirectory, "Quarantined", cancellationToken);
            return;
        }

        if (outcome.SkippedStale)
        {
            logger.LogWarning("{Tag} {File} ({FileDate}) is older than the last applied file; not applied",
                iface.LogTag, file.FileName, fileDate);

            await ledger.MarkSkippedStaleAsync(
                entry, outcome.StaleReason ?? "A newer file has already been applied.", cancellationToken);
            await MoveAsync(iface, file, processedDirectory, "Archived", cancellationToken);
            return;
        }

        logger.LogInformation("{Tag} {File}: {Summary}", iface.LogTag, file.FileName,
            outcome.Summary ?? $"received={outcome.Received}");

        await ledger.MarkSucceededAsync(
            entry, outcome.Received, outcome.Updated, outcome.Unchanged, cancellationToken);

        await MoveAsync(iface, file, processedDirectory, "Archived", cancellationToken);
    }

    /// <summary>
    /// Moves the file out of the inbox when the interface is configured to do so. Best-effort by
    /// design: the ledger already guarantees the file will not be reprocessed, so a failed move is
    /// housekeeping noise rather than a correctness problem — and on production it is the expected
    /// outcome, which is why <c>ProcessedDirectory</c> being null means "do not try".
    /// </summary>
    private async Task MoveAsync(
        InboundFileInterface iface,
        InboundFileInfo file,
        string? targetDirectory,
        string verb,
        CancellationToken cancellationToken)
    {
        if (targetDirectory is null)
            return;

        try
        {
            await fileSource.ArchiveAsync(file, targetDirectory, cancellationToken);
            logger.LogInformation("{Tag} {Verb} {File} → {Dir}", iface.LogTag, verb, file.FileName, targetDirectory);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{Tag} Could not move {File} to {Dir}; the ledger still prevents reprocessing",
                iface.LogTag, file.FileName, targetDirectory);
        }
    }
}
