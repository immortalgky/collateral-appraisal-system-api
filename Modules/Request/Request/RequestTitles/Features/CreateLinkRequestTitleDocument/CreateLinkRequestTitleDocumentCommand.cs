namespace Request.RequestTitles.Features.CreateLinkRequestTitleDocument;

public record CreateLinkRequestTitleDocumentCommand(
    Guid TitleId,
    RequestTitleDocumentDto RequestTitleDto
    ) : ICommand<CreateLinkRequestTitleDocumentResult>;