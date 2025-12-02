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
        // Make sure that linQ operations do not fail due to null reference
        var requestTitleDocs = command.RequestTitleDocumentDtos ?? new List<RequestTitleDocumentDto>();
        var requestTitleDocIds = requestTitleDocs
            .Where(dto => dto.Id!.HasValue && dto.Id!.Value != Guid.Empty)
            .Select(dto => dto.Id!.Value)
            .ToHashSet();

        var documentLinks = new List<DocumentLink>();

        var existingReqTitleDocsResult = await sender.Send(new GetLinkRequestTitleDocumentsByTitleIdQuery(command.TitleId), cancellationToken);

        var existingReqTitleDocs = existingReqTitleDocsResult.RequestTitleDocuments;
        var existingReqTitleDocIds = existingReqTitleDocs
            .Select(rt => rt.Id!.Value)
            .ToList();

        // Removing existing Request Title Documents that are not in the incoming list
        var removingReqTitleDocIds = existingReqTitleDocIds
            .Except(requestTitleDocIds)
            .ToList();
        var removingReqTitleDocs = existingReqTitleDocs
            .Where(rtd => removingReqTitleDocIds.Contains(rtd.Id!.Value))
            .ToList();

        // Creating Request Title Documents that do not have an Id
        var creatingReqTitleDocs = requestTitleDocs
            .Where(rtd => !rtd.Id.HasValue || rtd.Id!.Value == Guid.Empty)
            .ToList();

        // Updating Request Title Documents that have an Id
        var updatingReqTitleDocs = requestTitleDocs
            .Where(rtd => existingReqTitleDocs.Any(e => e.Id == rtd.Id && rtd.Id!.HasValue && rtd.Id != Guid.Empty && (
                rtd.DocumentId != e.DocumentId ||
                rtd.DocumentType != e.DocumentType ||
                rtd.Filename != e.Filename ||
                rtd.Prefix != e.Prefix ||
                rtd.Set != e.Set ||
                rtd.DocumentDescription != e.DocumentDescription ||
                rtd.FilePath != e.FilePath ||
                rtd.CreatedWorkstation != e.CreatedWorkstation ||
                rtd.IsRequired != e.IsRequired ||
                rtd.UploadedBy != e.UploadedBy ||
                rtd.UploadedByName != e.UploadedByName
                )))
            .ToList();
        var updatingReqTitleDocIds = updatingReqTitleDocs
            .Select(rtd => rtd.Id!.Value);

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
        }

        return new SyncRequestTitleDocumentsResult(true);
    }
}
