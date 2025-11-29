using System;
using MassTransit;
using Request.RequestTitles.Features.CreateLinkRequestTitleDocument;
using Request.RequestTitles.Features.GetLinkRequestTitleDocumentsByTitleId;
using Request.RequestTitles.Features.RemoveLinkRequestTitleDocument;
using Request.RequestTitles.Features.UpdateLinkRequestTitleDocument;
using Shared.Messaging.Events;

namespace Request.RequestTitles.Features.SyncRequestTitleDocuments;

public class SyncRequestTitleDocumentsHandler(ISender sender, IBus bus) : ICommandHandler<SyncRequestTitleDocumentsCommand, SyncRequestTitleDocumentsResult>
{
    public async Task<SyncRequestTitleDocumentsResult> Handle(SyncRequestTitleDocumentsCommand command, CancellationToken cancellationToken)
    {
        // Make sure that LinQ operation can work
        var requestTitleDocs = command.RequestTitleDocumentDtos ?? new List<RequestTitleDocumentDto>();

        var documentLinks = new List<DocumentLink>();
        var results = new List<RequestTitleDocumentDto>();

        var existingReqTitleDocsResult = await sender.Send(new GetLinkRequestTitleDocumentsByTitleIdQuery(command.TitleId), cancellationToken);

        var existingReqTitleDocs = existingReqTitleDocsResult.RequestTitleDocuments;
        var existingReqTitleDocIds = existingReqTitleDocs.Select(rt => rt.Id!.Value).ToList();

        var dtoIds = requestTitleDocs.Where(dto => dto.Id!.HasValue && dto.Id!.Value != Guid.Empty).Select(dto => dto.Id!.Value).ToList();
        var removingReqTitleDocIds = existingReqTitleDocIds.Except(dtoIds).ToList();
        var removingReqTitleDocs = existingReqTitleDocs.Where(rtd => removingReqTitleDocIds.Contains(rtd.Id!.Value)).ToList();

        var creatingReqTitleDocs = requestTitleDocs.Where(rtd => !rtd.Id.HasValue || rtd.Id!.Value == Guid.Empty).ToList();

        var updatingReqTitleDocs = requestTitleDocs.Where(rtd => rtd.Id.HasValue && rtd.Id != Guid.Empty).ToList();
        var updatingReqTitleDocIds = updatingReqTitleDocs.Select(rtd => rtd.Id!.Value);

        foreach (var reqTitleDoc in removingReqTitleDocs)
        {
            var removeResult = await sender.Send(new RemoveLinkRequestTitleDocumentByIdCommand(reqTitleDoc.Id!.Value, command.SessionId, reqTitleDoc.TitleId!.Value), cancellationToken);
        }

        foreach (var reqTitleDoc in creatingReqTitleDocs)
        {
            var createLinkRequestTitleDocumentCommand = new CreateLinkRequestTitleDocumentCommand
            {
                SessionId = command.SessionId,
                TitleId = command.TitleId,
                DocumentId = reqTitleDoc.DocumentId,
                DocumentType = reqTitleDoc.DocumentType,
                Filename = reqTitleDoc.Filename,
                Prefix = reqTitleDoc.Prefix,
                Set = reqTitleDoc.Set,
                DocumentDescription = reqTitleDoc.DocumentDescription,
                FilePath = reqTitleDoc.FilePath,
                CreatedWorkstation = reqTitleDoc.CreatedWorkstation,
                IsRequired = reqTitleDoc.IsRequired,
                UploadedBy = reqTitleDoc.UploadedBy,
                UploadedByName = reqTitleDoc.UploadedByName
            };

            var createLinkRequestTitleDocResult = await sender.Send(createLinkRequestTitleDocumentCommand, cancellationToken);

            if (reqTitleDoc.DocumentId.HasValue && reqTitleDoc.DocumentId != Guid.Empty)
            {
                documentLinks.Add(new DocumentLink
                {
                    EntityType = "Title",
                    EntityId = command.TitleId,
                    DocumentId = reqTitleDoc.DocumentId!.Value,
                    IsUnlinked = false
                });
            }

            results.Add(createLinkRequestTitleDocResult.Adapt<RequestTitleDocumentDto>());
        }

        foreach (var reqTitleDoc in updatingReqTitleDocs)
        {
            var updateLinkRequestTitleDocumentCommand = new UpdateLinkRequestTitleDocumentCommand
            {
                Id = reqTitleDoc.Id!.Value,
                SessionId = command.SessionId,
                TitleId = command.TitleId,
                DocumentId = reqTitleDoc.DocumentId,
                DocumentType = reqTitleDoc.DocumentType,
                Filename = reqTitleDoc.Filename,
                Prefix = reqTitleDoc.Prefix,
                Set = reqTitleDoc.Set,
                DocumentDescription = reqTitleDoc.DocumentDescription,
                FilePath = reqTitleDoc.FilePath,
                CreatedWorkstation = reqTitleDoc.CreatedWorkstation,
                IsRequired = reqTitleDoc.IsRequired,
                UploadedBy = reqTitleDoc.UploadedBy,
                UploadedByName = reqTitleDoc.UploadedByName
            };
            var updateLinkRequestTitleDocumentResult = await sender.Send(updateLinkRequestTitleDocumentCommand, cancellationToken);

            var existingDocId = existingReqTitleDocs.Where(etd => etd.Id == reqTitleDoc.Id).Select(ert => ert.DocumentId).First();

            if (!existingDocId.Equals(reqTitleDoc.DocumentId)) // update only upload document
            {
                documentLinks.Add(new DocumentLink
                {
                    EntityType = "Title",
                    EntityId = command.TitleId,
                    DocumentId = reqTitleDoc.DocumentId!.Value,
                    IsUnlinked = false
                });
            }

            results.Add(updateLinkRequestTitleDocumentResult.Adapt<RequestTitleDocumentDto>());
        }

        await bus.Publish(new DocumentLinkedIntegrationEvent
        {
            SessionId = command.SessionId,
            DocumentLinks = documentLinks
        }, cancellationToken);

        return new SyncRequestTitleDocumentsResult(results);
    }
}
