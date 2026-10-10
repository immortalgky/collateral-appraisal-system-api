namespace Document.Contracts.Documents.Dtos;

public record DocumentDto(
    Guid Id,
    string DocumentType,
    string DocumentCategory,
    string FileName,
    string FileExtension,
    long FileSizeBytes,
    string MimeType,
    string StorageUrl,
    string UploadedBy,
    string UploadedByName,
    DateTime UploadedAt,
    string? Description,
    bool IsActive
);
