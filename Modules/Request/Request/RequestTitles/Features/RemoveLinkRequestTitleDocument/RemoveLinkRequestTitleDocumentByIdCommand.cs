namespace Request.RequestTitles.Features.RemoveLinkRequestTitleDocument;

public record RemoveLinkRequestTitleDocumentByIdCommand(
    Guid Id,
    Guid TitleId
    ) : ICommand<RemoveLinkRequestTitleDocumentByIdResult>;