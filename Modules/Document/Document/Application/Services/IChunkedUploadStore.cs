namespace Document.Services;

/// <summary>
/// What an in-progress upload knows about itself. Written once when the upload is opened and
/// never updated: the only thing that changes while chunks arrive is the length of the data file,
/// and that is read from the file itself rather than tracked alongside it, so the two can never
/// disagree after a write that died halfway.
/// </summary>
public sealed record ChunkedUploadMeta(
    Guid UploadId,
    Guid UploadSessionId,
    string FileName,
    long FileSizeBytes,
    string ContentType,
    string DocumentType,
    string DocumentCategory,
    string? Description,
    string Owner,
    DateTime CreatedAt);

public enum ChunkAppendStatus
{
    Ok,

    /// <summary>No such upload — expired and swept, or never opened.</summary>
    NotFound,

    /// <summary>
    /// The offset sent is not where the file currently ends. The answer carries the real length so
    /// the client can carry on from there; it is how a retry after a dropped connection finds its
    /// place, and it needs no separate endpoint to ask.
    /// </summary>
    OffsetMismatch,

    /// <summary>
    /// Another request is writing to this upload. Both app nodes share the file, and SMB enforces
    /// the exclusive open across them, so this is what a second writer sees.
    /// </summary>
    Busy,

    /// <summary>The chunk would take the file past the size declared when it was opened.</summary>
    TooLarge
}

public readonly record struct ChunkAppendResult(ChunkAppendStatus Status, long ReceivedBytes);

/// <summary>An upload held for completion. Disposing releases it.</summary>
public sealed class ChunkedUploadClaim(ChunkedUploadMeta meta, IDisposable hold) : IDisposable
{
    public ChunkedUploadMeta Meta { get; } = meta;

    public void Dispose() => hold.Dispose();
}

/// <summary>
/// Holds uploads that arrive in pieces, as two files on the same share the documents live on:
/// <c>meta.json</c> and <c>data.part</c>. Deliberately not a database table — the bytes received
/// are the length of the data file, which is one fact in one place, where a stored offset would be
/// a second copy of it that a crash can leave stale.
/// </summary>
public interface IChunkedUploadStore
{
    Task CreateAsync(ChunkedUploadMeta meta, CancellationToken cancellationToken = default);

    Task<ChunkedUploadMeta?> TryReadMetaAsync(Guid uploadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes exclusive hold of an upload for the length of its completion, and reads its metadata
    /// through the same handle. Null when someone else holds it — another node, or the same
    /// client's earlier attempt still hashing a gigabyte.
    /// <para>
    /// Without this, a client that gives up waiting and retries completion starts a second run of
    /// the same work: the first has not committed its row yet, so the second sees nothing to
    /// return, does everything again, and collides on the primary key at the end.
    /// </para>
    /// Dispose the claim to release it.
    /// </summary>
    Task<ChunkedUploadClaim?> TryClaimAsync(Guid uploadId, CancellationToken cancellationToken = default);

    Task<ChunkAppendResult> AppendAsync(
        Guid uploadId,
        long offset,
        Stream body,
        long declaredFileSizeBytes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// How many bytes have arrived, read from the file itself. Null when there is no such upload.
    /// </summary>
    Task<long?> GetReceivedBytesAsync(Guid uploadId, CancellationToken cancellationToken = default);

    /// <summary>Where the assembled bytes live, for the completion step to hash and move.</summary>
    string DataPath(Guid uploadId);

    void Delete(Guid uploadId);

    /// <summary>Removes staging folders untouched for longer than <paramref name="olderThan"/>.</summary>
    int DeleteExpired(TimeSpan olderThan);
}
