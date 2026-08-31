namespace Integration.Infrastructure.FileInterface;

/// <summary>
/// Ledger of every inbound interface file this system has seen — one row per
/// (interface, file name, content hash).
///
/// <b>This is what prevents a file being ingested twice, not the archive step.</b> On production the
/// inbox is a drop zone we do not own: files are never moved away, so every run lists the same
/// accumulated files again. Relying on <c>ArchiveAsync</c> for de-duplication meant that a failed
/// (or impossible) move silently re-ingested the same file on every run — and once COLLATLINK became
/// a full replace, a single stale file could roll the whole table back to a previous month.
/// </summary>
public class InboundFileLog
{
    public Guid Id { get; private set; }

    /// <summary>Matches <see cref="FileInterfaceConfigEntity.InterfaceCode"/>.</summary>
    public string InterfaceCode { get; private set; } = null!;

    public string FileName { get; private set; } = null!;

    /// <summary>
    /// Date parsed out of the file name. Used to ORDER ingestion (oldest first) and nothing else —
    /// never to decide whether a file should be processed. AS400 builds its files around midnight, so
    /// the same batch can be stamped with either yesterday's or today's date.
    /// </summary>
    public DateOnly? FileDate { get; private set; }

    /// <summary>
    /// Size in bytes, from the directory listing. This is the cheap first-pass de-duplication key:
    /// it is known without downloading the file, which matters because the inbox keeps every file
    /// ever delivered and hashing them all would mean re-downloading hundreds of MB per run.
    /// </summary>
    public long SizeBytes { get; private set; }

    /// <summary>
    /// SHA-256 of the raw bytes. The authoritative key — catches a file re-sent under the same name
    /// and the same size but with corrected content, which the size check alone would skip.
    /// Null while a run is still in flight or when the file was rejected before it could be read.
    /// </summary>
    public string? ContentHash { get; private set; }

    public InboundFileStatus Status { get; private set; }

    public int RowsReceived { get; private set; }
    public int RowsUpdated { get; private set; }
    public int RowsUnchanged { get; private set; }

    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    /// <summary>Truncated to fit the column; full detail goes to the log.</summary>
    public string? ErrorMessage { get; private set; }

    private InboundFileLog() { }

    public static InboundFileLog Start(
        string interfaceCode,
        string fileName,
        DateOnly? fileDate,
        long sizeBytes,
        DateTime startedAt)
    {
        return new InboundFileLog
        {
            Id = Guid.CreateVersion7(),
            InterfaceCode = interfaceCode,
            FileName = fileName,
            FileDate = fileDate,
            SizeBytes = sizeBytes,
            Status = InboundFileStatus.InProgress,
            StartedAt = startedAt
        };
    }

    public void SetContentHash(string contentHash) => ContentHash = contentHash;

    public void MarkSucceeded(int received, int updated, int unchanged, DateTime completedAt)
    {
        Status = InboundFileStatus.Succeeded;
        RowsReceived = received;
        RowsUpdated = updated;
        RowsUnchanged = unchanged;
        CompletedAt = completedAt;
        ErrorMessage = null;
    }

    /// <summary>Transient failure — the file stays eligible for the next run.</summary>
    public void MarkFailed(string? error, DateTime completedAt)
    {
        Status = InboundFileStatus.Failed;
        CompletedAt = completedAt;
        ErrorMessage = Truncate(error);
    }

    /// <summary>Permanently unprocessable (bad name, bad layout) — never retried.</summary>
    public void MarkQuarantined(string? error, DateTime completedAt)
    {
        Status = InboundFileStatus.Quarantined;
        CompletedAt = completedAt;
        ErrorMessage = Truncate(error);
    }

    /// <summary>Rejected before ingestion because a newer file has already been applied.</summary>
    public void MarkSkippedStale(string? reason, DateTime completedAt)
    {
        Status = InboundFileStatus.SkippedStale;
        CompletedAt = completedAt;
        ErrorMessage = Truncate(reason);
    }

    private static string? Truncate(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Length <= 2000 ? s : s[..2000];
}

public enum InboundFileStatus
{
    InProgress = 0,
    Succeeded = 1,
    Failed = 2,
    Quarantined = 3,
    SkippedStale = 4
}
