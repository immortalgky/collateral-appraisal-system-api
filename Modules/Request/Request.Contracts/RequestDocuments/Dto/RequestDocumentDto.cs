namespace Request.Contracts.RequestDocuments.Dto;

public record RequestDocumentDto(
    Guid? Id,
    Guid RequestId,
    Guid? DocumentId,
    string DocumentType,
    string? FileName,
    string? Prefix,
    short? Set,
    string? Notes,
    string? FilePath,
    string? Source,
    bool IsRequired,
    string? UploadedBy,
    string? UploadedByName,
    DateTime? UploadedAt
);