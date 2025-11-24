namespace Request.RequestTitles.Features.UpdateLinkRequestTitleDocument;

public record UpdateLinkRequestTitleDocumentCommand(
  Guid TitleId,
  RequestTitleDocumentDto RequestTitleDocDto
  ) : ICommand<UpdateLinkRequestTitleDocumentResult>;