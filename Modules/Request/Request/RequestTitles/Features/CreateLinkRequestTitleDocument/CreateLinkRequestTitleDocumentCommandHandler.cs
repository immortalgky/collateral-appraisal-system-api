using MassTransit;
using Shared.Messaging.Events;

namespace Request.RequestTitles.Features.CreateLinkRequestTitleDocument;

public class CreateLinkRequestTitleDocumentCommandHandler(IRequestTitleRepository requestTitleRepository, IBus bus) : ICommandHandler<CreateLinkRequestTitleDocumentCommand, CreateLinkRequestTitleDocumentResult>
{
    public async Task<CreateLinkRequestTitleDocumentResult> Handle(CreateLinkRequestTitleDocumentCommand command, CancellationToken cancellationToken)
    {
        var requestTitle = await requestTitleRepository.GetByIdAsync(command.TitleId, cancellationToken);
        
        if (requestTitle == null)
            throw new RequestTitleNotFoundException(command.TitleId);

        var requestTitleDocument = requestTitle.CreateLinkRequestTitleDocument(command.Adapt<RequestTitleDocumentData>() with { TitleId = command.TitleId });

        if (command.DocumentId.HasValue && command.DocumentId != Guid.Empty)
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
                        DocumentId = command.DocumentId!.Value,
                        IsUnlinked = false
                    }
                }
            }, cancellationToken);
        }

        await requestTitleRepository.SaveChangesAsync();
        
        return new CreateLinkRequestTitleDocumentResult { 
            Id = requestTitleDocument.Id,
            TitleId = requestTitleDocument.TitleId,
            DocumentId = requestTitleDocument.DocumentId,
            DocumentType = requestTitleDocument.DocumentType,
            Filename = requestTitleDocument.Filename,
            Prefix = requestTitleDocument.Prefix,
            Set = requestTitleDocument.Set,
            DocumentDescription = requestTitleDocument.DocumentDescription,
            FilePath = requestTitleDocument.FilePath,
            CreatedWorkstation = requestTitleDocument.CreatedWorkstation,
            IsRequired = requestTitleDocument.IsRequired,
            UploadedBy = requestTitleDocument.UploadedBy,
            UploadedByName = requestTitleDocument.UploadedByName
            };
    }
}