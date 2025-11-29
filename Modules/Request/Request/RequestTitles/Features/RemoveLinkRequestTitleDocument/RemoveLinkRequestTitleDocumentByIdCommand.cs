namespace Request.RequestTitles.Features.RemoveLinkRequestTitleDocument;

public record RemoveLinkRequestTitleDocumentByIdCommand(
    Guid Id,
    Guid SessionId,
    Guid TitleId
    ) : ICommand<RemoveLinkRequestTitleDocumentByIdResult>;