namespace Request.RequestTitles.Features.GetLinkRequestTitleDocumentById;

public record GetLinkRequestTitleDocumentByIdResult(
  Guid? Id,
  Guid? TitleId,
  Guid? DocumentId,
  string DocumentType,
  bool IsRequired,
  string DocumentDescription,
  string UploadedBy,
  string UploadedByName
  );