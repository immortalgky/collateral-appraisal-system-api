using Integration.Contracts.FileSource;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Time;
using System.Security.Cryptography;

namespace Integration.Infrastructure.FileInterface;

/// <summary>
/// Decides which inbound files still need work, and records what happened to each one.
///
/// <b>Why this exists.</b> De-duplication used to be a side effect of archiving: a file was moved out
/// of the inbox once ingested, so the next run simply did not see it. On production we do not own the
/// drop folder and cannot move anything, which left every past file visible on every run — and once
/// COLLATLINK became a full replace, re-ingesting a stale file would roll the table back to an
/// earlier month. The ledger takes over that job so archiving becomes optional.
///
/// <b>Two passes, on purpose.</b> The authoritative key is the content hash, but computing it means
/// downloading the file. With a backlog that never shrinks that would be hundreds of MB pulled over
/// SFTP every run to conclude "nothing new". So the cheap pass (name + size, both already in the
/// directory listing) removes the backlog first, and only what survives is downloaded and hashed.
/// The hash pass still runs, because a file re-sent under the same name and size but with corrected
/// content must not be skipped.
/// </summary>
public class InboundFileLedger(
    IntegrationDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<InboundFileLedger> logger)
{
    /// <summary>IN-clause chunk size, matching the other AS400 ingestors.</summary>
    private const int BatchSize = 1000;

    /// <summary>
    /// Drops files the ledger is finished with (<see cref="InboundFileLog.FinishedStatuses"/>), judged
    /// by name + size alone so nothing is read. Failed or interrupted files stay in, to be retried.
    ///
    /// A file quarantined for its CONTENT stays in too: AS400 files are fixed-width, so a corrected
    /// re-send keeps the same name and size and only the hash can tell it apart. The runner checks
    /// <see cref="IsQuarantinedContentAsync"/> before touching the ledger, so the unchanged bad file
    /// costs a download and nothing else. A file quarantined for its NAME (no hash) is dropped here.
    /// Everything returned still has to pass <see cref="TryClaimAsync"/> once its bytes are known.
    /// </summary>
    public async Task<IReadOnlyList<InboundFileInfo>> FilterUnprocessedAsync(
        string interfaceCode,
        IReadOnlyList<InboundFileInfo> files,
        CancellationToken cancellationToken = default)
    {
        if (files.Count == 0)
            return files;

        var seen = new HashSet<(string Name, long Size)>();

        foreach (var chunk in files.Select(f => f.FileName).Distinct(StringComparer.Ordinal).Chunk(BatchSize))
        {
            var rows = await dbContext.InboundFileLogs
                .AsNoTracking()
                .Where(l => l.InterfaceCode == interfaceCode
                            && InboundFileLog.FinishedStatuses.Contains(l.Status)
                            && !(l.Status == InboundFileStatus.Quarantined && l.ContentHash != null)
                            && chunk.Contains(l.FileName))
                .Select(l => new { l.FileName, l.SizeBytes })
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
                seen.Add((row.FileName, row.SizeBytes));
        }

        var pending = files.Where(f => !seen.Contains((f.FileName, f.SizeBytes))).ToList();

        if (pending.Count != files.Count)
            logger.LogInformation(
                "[InboundFileLedger] {Code}: {Skipped} of {Total} file(s) already handled; {Pending} to process",
                interfaceCode, files.Count - pending.Count, files.Count, pending.Count);

        return pending;
    }

    /// <summary>
    /// Opens the ledger row for this attempt. Call before ingesting so a crash mid-ingest still leaves a
    /// trace of what was being processed. (The runner reads and hashes the file first, to recognise an
    /// already-quarantined file without touching the ledger; a crash while downloading leaves nothing,
    /// and the next run simply tries again.)
    ///
    /// A file that failed or was interrupted before is retried on its existing row rather than a new
    /// one: a second row would carry the same (interface, file name, hash) the moment the content is
    /// hashed, and the unique index would reject it — the retry would never get through. A finished
    /// row is never reopened, so a file re-sent under the same name after succeeding gets its own row
    /// and the earlier success stays on record.
    /// </summary>
    public async Task<InboundFileLog> BeginAsync(
        string interfaceCode,
        InboundFileInfo file,
        DateOnly? fileDate,
        CancellationToken cancellationToken = default)
    {
        var now = dateTimeProvider.ApplicationNow;

        var entry = await dbContext.InboundFileLogs
            .Where(l => l.InterfaceCode == interfaceCode
                        && l.FileName == file.FileName
                        && !InboundFileLog.FinishedStatuses.Contains(l.Status))
            .OrderByDescending(l => l.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (entry is null)
        {
            entry = InboundFileLog.Start(interfaceCode, file.FileName, fileDate, file.SizeBytes, now);
            dbContext.InboundFileLogs.Add(entry);
        }
        else
        {
            entry.Reopen(file.SizeBytes, fileDate, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return entry;
    }

    /// <summary>
    /// Stamps the content hash and reports whether this exact content is new.
    ///
    /// Returns <c>false</c> when a finished row already holds the same (interface, file name, hash) —
    /// the file survived the size check but is byte-identical to one the ledger is done with, so
    /// ingestion must be skipped. The open row is closed as <see cref="InboundFileStatus.SkippedStale"/>
    /// WITHOUT the hash: the unique index allows only one row per content, and it is taken.
    ///
    /// An unfinished row holding the hash is an earlier attempt at this same content (possible when a
    /// crash left several rows for one file). It gives the hash up so this attempt can carry it;
    /// otherwise the save below would violate the unique index and the file could never be retried.
    /// </summary>
    public async Task<bool> TryClaimAsync(
        InboundFileLog entry,
        string contentHash,
        CancellationToken cancellationToken = default)
    {
        var holders = dbContext.InboundFileLogs
            .Where(l => l.Id != entry.Id
                        && l.InterfaceCode == entry.InterfaceCode
                        && l.FileName == entry.FileName
                        && l.ContentHash == contentHash);

        var duplicate = await holders
            .AnyAsync(l => InboundFileLog.FinishedStatuses.Contains(l.Status), cancellationToken);

        if (duplicate)
        {
            logger.LogInformation(
                "[InboundFileLedger] {Code}: {File} is byte-identical to a file already handled; skipping",
                entry.InterfaceCode, entry.FileName);

            entry.MarkSkippedStale("Content already handled under the same file name.",
                dateTimeProvider.ApplicationNow);
        }
        else
        {
            await holders.ExecuteUpdateAsync(
                s => s.SetProperty(l => l.ContentHash, (string?)null), cancellationToken);

            entry.SetContentHash(contentHash);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return !duplicate;
    }

    /// <summary>
    /// Whether these exact bytes were already quarantined under this file name — the same bad file
    /// still sitting in the drop folder. Checked before a ledger row is opened, so re-seeing it every
    /// run neither re-parses it nor adds a row.
    /// </summary>
    public Task<bool> IsQuarantinedContentAsync(
        string interfaceCode, string fileName, string contentHash, CancellationToken cancellationToken = default) =>
        dbContext.InboundFileLogs.AnyAsync(l => l.InterfaceCode == interfaceCode
                                                && l.FileName == fileName
                                                && l.ContentHash == contentHash
                                                && l.Status == InboundFileStatus.Quarantined,
            cancellationToken);

    public Task MarkSucceededAsync(
        InboundFileLog entry, int received, int updated, int unchanged,
        CancellationToken cancellationToken = default)
    {
        entry.MarkSucceeded(received, updated, unchanged, dateTimeProvider.ApplicationNow);
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Transient failure — the file stays eligible, and the next run retries it on this row.</summary>
    public Task MarkFailedAsync(InboundFileLog entry, string? error, CancellationToken cancellationToken = default)
    {
        entry.MarkFailed(error, dateTimeProvider.ApplicationNow);
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Permanently unprocessable — the ledger row is what stops it being retried forever.</summary>
    public Task MarkQuarantinedAsync(InboundFileLog entry, string? error, CancellationToken cancellationToken = default)
    {
        entry.MarkQuarantined(error, dateTimeProvider.ApplicationNow);
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Rejected because a newer file has already been applied.</summary>
    public Task MarkSkippedStaleAsync(InboundFileLog entry, string? reason, CancellationToken cancellationToken = default)
    {
        entry.MarkSkippedStale(reason, dateTimeProvider.ApplicationNow);
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Reads the whole stream into memory and returns both the bytes and their SHA-256.
    ///
    /// Buffering is deliberate: the stream has to be hashed AND parsed, SFTP streams are not
    /// seekable, and these files are a few MB — small enough that a second download costs more than
    /// the memory.
    /// </summary>
    public static async Task<(byte[] Content, string Hash)> ReadAndHashAsync(
        Stream stream, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);

        var bytes = buffer.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(bytes));

        return (bytes, hash);
    }
}
