namespace Integration.Contracts.FileSource;

/// <summary>
/// Abstraction over the transport layer that delivers inbound interface files (e.g. AS400 COLLATREV).
/// Switched by config: <c>FileTransfer:Inbound:FileSource = Local | Sftp</c>.
/// Lives in Integration.Contracts so consuming modules (e.g. Collateral) depend on the port only.
/// </summary>
public interface IInboundFileSource
{
    /// <summary>
    /// Returns the list of files available for ingestion.
    /// For Local: files in <paramref name="directory"/> matching <paramref name="filePattern"/>.
    /// For SFTP: files in the remote <paramref name="directory"/>.
    /// </summary>
    Task<IReadOnlyList<InboundFileInfo>> ListFilesAsync(string directory, string filePattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a read stream for the given file.
    /// The caller is responsible for disposing the stream.
    /// </summary>
    Task<Stream> OpenReadAsync(InboundFileInfo file, CancellationToken cancellationToken = default);

    /// <summary>
    /// Archives the file after successful ingestion (moves to <paramref name="processedDirectory"/> /
    /// SFTP processed dir).
    /// </summary>
    Task ArchiveAsync(InboundFileInfo file, string processedDirectory, CancellationToken cancellationToken = default);
}

/// <summary>
/// Metadata about an inbound file available for ingestion.
/// </summary>
/// <param name="FileName">Display name / filename without path (e.g. AS400_COLLATREV_20260501.txt).</param>
/// <param name="FullPath">
/// Full path or remote path used internally by the provider.
/// Do not expose this to the caller beyond the file-source layer.
/// </param>
/// <param name="SizeBytes">
/// Size from the directory listing, known WITHOUT reading the file. The ingest jobs use it as a
/// cheap first-pass de-duplication key: on production the inbox is never cleared, so every run sees
/// every file ever delivered, and hashing them all would mean re-downloading the whole backlog.
/// </param>
/// <param name="LastModified">Last write time reported by the provider; null when unavailable.</param>
public record InboundFileInfo(
    string FileName,
    string FullPath,
    long SizeBytes = 0,
    DateTime? LastModified = null);
