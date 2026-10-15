namespace Document.Domain.Documents.Features.ChunkedUpload;

/// <summary>
/// Opens an upload that will arrive in pieces. Everything the finished document needs is declared
/// here, so a file that would be refused — wrong extension, too large, a session that has already
/// closed — is refused before a single byte of it is sent.
/// </summary>
public record InitChunkedUploadCommand(
    Guid UploadSessionId,
    string FileName,
    long FileSizeBytes,
    string ContentType,
    string DocumentType,
    string DocumentCategory,
    string? Description
) : ICommand<InitChunkedUploadResult>;

public record InitChunkedUploadResult(Guid UploadId);
