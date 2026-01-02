namespace Request.Application.Features.RequestTitles.GetTitleDocumentById;

public record GetLinkRequestTitleDocumentByIdQuery(Guid RequestId, Guid TitleId, Guid TitleDocId) : IQuery<GetLinkRequestTitleDocumentByIdResult>;