using MassTransit;
using Shared.Messaging.Events;

namespace Request.RequestTitles.Features.UpdateLinkRequestTitleDocument;

public class UpdateLinkRequestTitleDocumentCommandHandler(IRequestTitleDocumentRepository requestTitleDocumentRepository, IBus bus) : ICommandHandler<UpdateLinkRequestTitleDocumentCommand, UpdateLinkRequestTitleDocumentResult>
{
    public async Task<UpdateLinkRequestTitleDocumentResult> Handle(UpdateLinkRequestTitleDocumentCommand command, CancellationToken cancellationToken)
    {
        var documentLinks = new List<DocumentLink>();

        var existingRequestTitleDoc = await requestTitleDocumentRepository.GetRequestTitleDocumentByIdAsync(command.Id, cancellationToken);
        
        if (existingRequestTitleDoc is null)
            throw new RequestTitleDocumentNotFoundException(command.Id);

        if ((!existingRequestTitleDoc.DocumentId.HasValue || existingRequestTitleDoc.DocumentId == Guid.Empty) && command.DocumentId.HasValue && command.DocumentId != Guid.Empty)
        {
            // add new link
            documentLinks.Add(
                new DocumentLink
                {
                    EntityType = "Title",
                    EntityId = command.TitleId,
                    DocumentId = command.DocumentId!.Value,
                    IsUnlinked = false
                }
                );
        }

        if (existingRequestTitleDoc.DocumentId.HasValue && existingRequestTitleDoc.DocumentId != Guid.Empty && command.DocumentId.HasValue && command.DocumentId != Guid.Empty)
        {
            // unlink existing
            documentLinks.Add(
                new DocumentLink
                {
                    EntityType = "Title",
                    EntityId = command.TitleId,
                    DocumentId = existingRequestTitleDoc.DocumentId!.Value,
                    IsUnlinked = true
                }
                );
            // add new link
            documentLinks.Add(
                new DocumentLink
                {
                    EntityType = "Title",
                    EntityId = command.TitleId,
                    DocumentId = command.DocumentId!.Value,
                    IsUnlinked = false
                }
                );

        }

        await bus.Publish(new DocumentLinkedIntegrationEvent
        {
            SessionId = command.SessionId,
            DocumentLinks = documentLinks
        }, cancellationToken);

        existingRequestTitleDoc.Update(new RequestTitleDocumentData
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
            Id = existingRequestTitleDoc.Id,
            TitleId = existingRequestTitleDoc.TitleId,
            DocumentId = existingRequestTitleDoc.DocumentId,
            DocumentType = existingRequestTitleDoc.DocumentType,
            Filename = existingRequestTitleDoc.Filename,
            Prefix = existingRequestTitleDoc.Prefix,
            Set = existingRequestTitleDoc.Set,
            DocumentDescription = existingRequestTitleDoc.DocumentDescription,
            FilePath = existingRequestTitleDoc.FilePath,
            CreatedWorkstation = existingRequestTitleDoc.CreatedWorkstation,
            IsRequired = existingRequestTitleDoc.IsRequired,
            UploadedBy = existingRequestTitleDoc.UploadedBy,
            UploadedByName = existingRequestTitleDoc.UploadedByName
        };
    }
}