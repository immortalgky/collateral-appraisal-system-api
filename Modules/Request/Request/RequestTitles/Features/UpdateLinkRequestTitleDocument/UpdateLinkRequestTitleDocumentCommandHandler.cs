using MassTransit;
using Shared.Messaging.Events;

namespace Request.RequestTitles.Features.UpdateLinkRequestTitleDocument;

public class UpdateLinkRequestTitleDocumentCommandHandler(IRequestTitleDocumentRepository requestTitleDocumentRepository, IBus bus) : ICommandHandler<UpdateLinkRequestTitleDocumentCommand, UpdateLinkRequestTitleDocumentResult>
{
    public async Task<UpdateLinkRequestTitleDocumentResult> Handle(UpdateLinkRequestTitleDocumentCommand command, CancellationToken cancellationToken)
    {
        var requestTitleDocument = await requestTitleDocumentRepository.GetRequestTitleDocumentByIdAsync(command.Id, cancellationToken);
        
        if (requestTitleDocument is null)
            throw new RequestTitleDocumentNotFoundException(command.Id);

        if (!requestTitleDocument.DocumentId.Equals(command.DocumentId) && (!requestTitleDocument.DocumentId.HasValue || requestTitleDocument.DocumentId == Guid.Empty))
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
        
        requestTitleDocument.Update(new RequestTitleDocumentData
        {
            DocumentId = command.DocumentId,
            DocumentType = command.DocumentType,
            Filename = command.Filename,
            Prefix = command.Prefix,
            Set = command.Set,
            DocumentDescription = command.DocumentDescription,
            FilePath = command.FilePath,
            CreatedWorkstation = command.CreatedWorkstation,
            IsRequired = command.IsRequired,
            UploadedBy = command.UploadedBy,
            UploadedByName = command.UploadedByName
        });


        await requestTitleDocumentRepository.SaveChangeAsync(cancellationToken);

        return new UpdateLinkRequestTitleDocumentResult
        {
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