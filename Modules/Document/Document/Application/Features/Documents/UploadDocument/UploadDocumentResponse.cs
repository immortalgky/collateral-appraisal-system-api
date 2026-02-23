namespace Document.Domain.Documents.Features.UploadDocument;

public record UploadDocumentResponse(
    Guid DocumentId,
    string FileName,
    string FileExtension,
    long FileSize,
    string MimeType,
    string StorageUrl,
    string DocumentType,
    string DocumentCategory,
    string? Description,
    string UploadedBy,
    string UploadedByName,
    DateTime UploadedAt
);
