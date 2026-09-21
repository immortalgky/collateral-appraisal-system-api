namespace Document.Services;

/// <summary>
/// A file written to the staging area and not yet a document: the bytes are on the share, nothing
/// in the database points at them, and the id they will be filed under is already decided.
/// </summary>
public sealed record StagedUpload(Guid DocumentId, string StagedPath, long Length, string ChecksumBase64);

/// <summary>
/// What a staged file needs to know about itself before it can become a document. Plain values
/// rather than the chunked upload's own record, because a streamed upload has no such record — it
/// learns these from the parts of the request that arrive alongside the file.
/// </summary>
public sealed record StagedFileMetadata(
    Guid UploadSessionId,
    string FileName,
    long FileSizeBytes,
    string ContentType,
    string DocumentType,
    string DocumentCategory,
    string? Description);
