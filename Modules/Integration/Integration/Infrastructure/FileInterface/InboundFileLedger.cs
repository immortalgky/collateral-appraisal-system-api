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
    /// Drops files already ingested successfully, judged by name + size alone so nothing is read.
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
                            && l.Status == InboundFileStatus.Succeeded
                            && chunk.Contains(l.FileName))
                .Select(l => new { l.FileName, l.SizeBytes })
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
                seen.Add((row.FileName, row.SizeBytes));
        }

        var pending = files.Where(f => !seen.Contains((f.FileName, f.SizeBytes))).ToList();

        if (pending.Count != files.Count)
            logger.LogInformation(
                "[InboundFileLedger] {Code}: {Skipped} of {Total} file(s) already ingested; {Pending} to process",
                interfaceCode, files.Count - pending.Count, files.Count, pending.Count);

        return pending;
    }

    /// <summary>
    /// Opens a ledger row for this attempt. Call before reading the file so a crash mid-ingest still
    /// leaves a trace of what was being processed.
    /// </summary>
    public async Task<InboundFileLog> BeginAsync(
        string interfaceCode,
        InboundFileInfo file,
        DateOnly? fileDate,
        CancellationToken cancellationToken = default)
    {
        var entry = InboundFileLog.Start(
            interfaceCode, file.FileName, fileDate, file.SizeBytes, dateTimeProvider.ApplicationNow);

        dbContext.InboundFileLogs.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);

        return entry;
    }

    /// <summary>
    /// Stamps the content hash and reports whether this exact content is new.
    ///
    /// Returns <c>false</c> when the same (interface, file name, hash) has already succeeded — the
    /// file survived the size check but is byte-identical to one we have done, so ingestion must be
    /// skipped. The open ledger row is closed as <see cref="InboundFileStatus.SkippedStale"/>.
    /// </summary>
    public async Task<bool> TryClaimAsync(
        InboundFileLog entry,
        string contentHash,
        CancellationToken cancellationToken = default)
    {
        var duplicate = await dbContext.InboundFileLogs
            .AsNoTracking()
            .AnyAsync(l => l.Id != entry.Id
                           && l.InterfaceCode == entry.InterfaceCode
                           && l.FileName == entry.FileName
                           && l.ContentHash == contentHash
                           && l.Status == InboundFileStatus.Succeeded,
                cancellationToken);

        entry.SetContentHash(contentHash);

        if (duplicate)
        {
            logger.LogInformation(
                "[InboundFileLedger] {Code}: {File} is byte-identical to a file already ingested; skipping",
                entry.InterfaceCode, entry.FileName);

            entry.MarkSkippedStale("Content already ingested under the same file name.",
                dateTimeProvider.ApplicationNow);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return !duplicate;
    }

    public Task MarkSucceededAsync(
        InboundFileLog entry, int received, int updated, int unchanged,
        CancellationToken cancellationToken = default)
    {
        entry.MarkSucceeded(received, updated, unchanged, dateTimeProvider.ApplicationNow);
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Transient failure — the file stays eligible, so the next run retries it.</summary>
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
