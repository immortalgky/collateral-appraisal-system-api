namespace Request.Application.Features.RequestTitles.GetTitleDocumentById;

public record GetLinkRequestTitleDocumentByIdResult(
    Guid? Id,
    Guid? TitleId,
    Guid? DocumentId,
    string DocumentType,
    string? Filename,
    string Prefix,
    int Set,
    string? DocumentDescription,
    string? FilePath,
    string? CreatedWorkstation,
    bool IsRequired,
    string? UploadedBy,
    string? UploadedByName
    );