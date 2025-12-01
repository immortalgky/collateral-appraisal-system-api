namespace Request.Contracts.RequestDocuments.Dto;

public record RequestDocumentDto(
    Guid? Id,
    Guid? DocumentId,
    string? FileName,
    string? Prefix,
    short? Set,
    string? FilePath,
    bool DocumentFollowUp,
    DocumentClassificationDto DocumentClassification,
    UploadInfoDto UploadInfo,
    string? DocumentDescription
);