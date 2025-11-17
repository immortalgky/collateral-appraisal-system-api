namespace Request.Contracts.RequestDocuments.Dto;

public record RequestDocumentDto(
    Guid RequestId,
    Guid DocumentId,
    DocumentClassificationDto DocumentClassification,
    UploadInfoDto UploadInfo,
    string? DocumentDescription
);