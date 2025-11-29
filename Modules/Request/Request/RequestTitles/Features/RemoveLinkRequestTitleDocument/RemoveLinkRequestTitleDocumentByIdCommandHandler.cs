using MassTransit;
using Shared.Messaging.Events;

namespace Request.RequestTitles.Features.RemoveLinkRequestTitleDocument;

public class RemoveLinkRequestTitleDocumentByIdCommandHandler(IRequestTitleDocumentRepository requestTitleDocumentRepository, IBus bus) : ICommandHandler<RemoveLinkRequestTitleDocumentByIdCommand, RemoveLinkRequestTitleDocumentByIdResult>
{
    public async Task<RemoveLinkRequestTitleDocumentByIdResult> Handle(RemoveLinkRequestTitleDocumentByIdCommand command, CancellationToken cancellationToken)
    {
        var requestTitleDocument = await requestTitleDocumentRepository.GetRequestTitleDocumentByIdAsync(command.Id);
        
        if (requestTitleDocument is null)
            throw new RequestTitleDocumentNotFoundException(command.Id);

        if (requestTitleDocument.TitleId != command.TitleId)
            throw new Exception($"RequestId unmatch {requestTitleDocument.TitleId} : {command.TitleId}");

        if (requestTitleDocument.DocumentId.HasValue && requestTitleDocument.DocumentId != Guid.Empty)
        {
            await bus.Publish(new DocumentLinkedIntegrationEvent
            {
                SessionId = command.SessionId,
                DocumentLinks = new List<DocumentLink>()
                {
                    new DocumentLink
                    {
                        EntityType = "Title",
                        EntityId = command.TitleId,
                        DocumentId = requestTitleDocument.DocumentId!.Value,
                        IsUnlinked = false
                    }
                }
            }, cancellationToken);
        }

        await requestTitleDocumentRepository.Remove(requestTitleDocument);
        
        await requestTitleDocumentRepository.SaveChangeAsync(cancellationToken);
        
        return new RemoveLinkRequestTitleDocumentByIdResult(true);
    }
}