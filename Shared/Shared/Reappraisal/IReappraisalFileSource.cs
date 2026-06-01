namespace Shared.Reappraisal;

/// <summary>
/// Abstraction over the transport layer that delivers AS400 COLLATREV files.
/// Switched by config: <c>Reappraisal:FileSource = Local | Sftp</c>.
/// </summary>
public interface IReappraisalFileSource
{
    /// <summary>
    /// Returns the list of COLLATREV files available for ingestion.
    /// For Local: files in the configured directory.
    /// For SFTP: files in the configured remote directory.
    /// </summary>
    Task<IReadOnlyList<ReappraisalFileInfo>> ListFilesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a read stream for the given file.
    /// The caller is responsible for disposing the stream.
    /// </summary>
    Task<Stream> OpenReadAsync(ReappraisalFileInfo file, CancellationToken cancellationToken = default);

    /// <summary>
    /// Archives the file after successful ingestion (moves to processed sub-directory / SFTP processed dir).
    /// </summary>
    Task ArchiveAsync(ReappraisalFileInfo file, CancellationToken cancellationToken = default);
}

/// <summary>
/// Metadata about a COLLATREV file available for ingestion.
/// </summary>
public record ReappraisalFileInfo(
    /// <summary>Display name / filename without path (e.g. AS400_COLLATREV_20260501.txt).</summary>
    string FileName,

    /// <summary>
    /// Full path or remote path used internally by the provider.
    /// Do not expose this to the caller beyond the file-source layer.
    /// </summary>
    string FullPath
);
