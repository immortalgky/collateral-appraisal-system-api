namespace Request.Contracts.RequestDocuments.Dto;

public record DocumentClassificationDto(
    string DocumentType,
    bool IsRequired
);