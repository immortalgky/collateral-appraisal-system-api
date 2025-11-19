namespace Request.Contracts.Requests.Dtos;
public record RequestTitleDocumentDto(
    Guid? Id,
    Guid? TitleId,
    Guid DocumentId,
    string? DocumentType,
    bool IsRequired,
    string? DocumentDescription,
    string? UploadedBy,
    string? UploadedByName
    );