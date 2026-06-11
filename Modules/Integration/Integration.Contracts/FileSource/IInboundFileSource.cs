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
public record InboundFileInfo(
    /// <summary>Display name / filename without path (e.g. AS400_COLLATREV_20260501.txt).</summary>
    string FileName,

    /// <summary>
    /// Full path or remote path used internally by the provider.
    /// Do not expose this to the caller beyond the file-source layer.
    /// </summary>
    string FullPath
);
