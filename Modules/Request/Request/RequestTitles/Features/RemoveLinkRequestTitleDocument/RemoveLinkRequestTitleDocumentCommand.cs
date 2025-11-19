namespace Request.RequestTitles.Features.RemoveLinkRequestTitleDocument;

public record RemoveLinkRequestTitleDocumentCommand(
    Guid Id,
    Guid TitleId
    ) : ICommand<RemoveLinkRequestTitleDocumentResult>;