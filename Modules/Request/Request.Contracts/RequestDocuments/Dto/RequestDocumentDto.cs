namespace Request.Contracts.RequestDocuments.Dto;

public record RequestDocumentDto(
    Guid DocumentId,
    DocumentClassificationDto DocumentClassification,
    UploadInfoDto UploadInfo,
    string? DocumentDescription
);