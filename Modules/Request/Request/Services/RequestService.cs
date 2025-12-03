using MassTransit;
using Request.RequestDocuments.Features.AddRequestDocument;
using Request.Requests.Features.CreateRequest;
using Shared.Messaging.Events;
using Request.Requests.Features.UpdateRequest;
using Request.Requests.Features.CreateDraftRequest;
using Request.Requests.Features.UpdateDraftRequest;
using Request.RequestDocuments.Features.GetRequestDocument;
using Request.RequestDocuments.Features.RemoveRequestDocument;
using Request.Requests.Features.DeleteRequest;
using Request.RequestDocuments.Features.UpdateRequestDocument;
using Request.RequestTitles.Features.SyncRequestTitles;
using Request.RequestComments.Features.SyncRequestComments;
using Request.RequestTitles.Features.SyncDraftRequestTitles;

namespace Request.Services;

public class RequestService(IBus bus) : IRequestService
{
    public async Task<CreateRequestResult> CreateRequestAsync(
        RequestDto request, ISender sender,
        CancellationToken cancellationToken)
    {
        // Create Request
        var requestCommand = request.Adapt<CreateRequestCommand>();
        var requestResult = await sender.Send(requestCommand, cancellationToken);

        // Add Request Documents
        var requestDocCommand = new AddRequestDocumentCommand(requestResult.Id, request.Documents);
        await sender.Send(requestDocCommand, cancellationToken);

        // Add Request Title
        var requestTitleResult = await sender.Send(new SyncRequestTitlesCommand
        {
            SessionId = request.SessionId,
            RequestId = requestResult.Id,
            RequestTitleDtos = request.Titles
        }, cancellationToken);

        // Add Request Comments
        var requestCustomer = await sender.Send(new SyncRequestCommentsCommand {
            RequestId = requestResult.Id,
            RequestCommentDtos = request.Comments
        }, cancellationToken);

        // IntegrationEvent
        var integrationEvent = new DocumentLinkedIntegrationEvent
        {
            SessionId = request.SessionId,
            DocumentLinks = request.Documents
                .Where(d => d.DocumentId != null)
                .Select(d => new DocumentLink
                {
                    EntityType = "request",
                    EntityId = requestResult.Id,
                    DocumentId = d.DocumentId!.Value
                })
                .ToList()
        };

        if (integrationEvent.DocumentLinks.Any())
        {
            await bus.Publish(integrationEvent, cancellationToken);
        }

        return new CreateRequestResult(requestResult.Id);
    }

    public async Task<CreateDraftRequestResult> CreateRequestDraftAsync(RequestDto request, ISender sender,
        CancellationToken cancellationToken)
    {
        // Create Request
        var requestCommand = request.Adapt<CreateDraftRequestCommand>();
        var requestResult = await sender.Send(requestCommand, cancellationToken);

        // Sync Request Titles
        var requestTitleResult = await sender.Send(new SyncDraftRequestTitlesCommand
        {
            SessionId = request.SessionId,
            RequestId = requestResult.Id,
            RequestTitleDtos = request.Titles
        }, cancellationToken);

        // Add Request Documents
        var requestDocCommand = new AddRequestDocumentCommand(requestResult.Id, request.Documents);
        await sender.Send(requestDocCommand, cancellationToken);

        // Add Request Comments
        var requestCustomer = await sender.Send(new SyncRequestCommentsCommand
        {
            RequestId = requestResult.Id,
            RequestCommentDtos = request.Comments
        }, cancellationToken);

        // IntregationEvent
        var integrationEvent = new DocumentLinkedIntegrationEvent
        {
            SessionId = request.SessionId,
            DocumentLinks = request.Documents
                .Where(d => d.DocumentId != null)
                .Select(d => new DocumentLink
                {
                    EntityType = "request",
                    EntityId = requestResult.Id,
                    DocumentId = d.DocumentId!.Value,
                    IsUnlink = false
                })
                .ToList()
        };
        if (integrationEvent.DocumentLinks.Any())
        {
            await bus.Publish(integrationEvent, cancellationToken);
        }

        return new CreateDraftRequestResult(requestResult.Id);
    }

    // Delete Request
    public async Task<DeleteRequestResult> DeleteRequestAsync(Guid id, Guid sessionId, ISender sender,
        CancellationToken cancellationToken)
    {
        // Make sure Request Titles Documents are synced and events are published before deleting the Request
        var syncRequestTitleResult = await sender.Send(new SyncRequestTitlesCommand
        {
            SessionId = sessionId,
            RequestId = id
        }, cancellationToken);

        // Make sure Request Comments are synced before deleting the Request
        var syncRequestCommentResult = await sender.Send(new SyncRequestCommentsCommand
        {
            RequestId = id
        }, cancellationToken);

        var result = await sender.Send(new DeleteRequestCommand(id), cancellationToken);
        var requestDocCommand = new GetRequestDocumentQuery(id);
        var requestDocResult = await sender.Send(requestDocCommand, cancellationToken);

        var eventDocs = new List<DocumentLink>();

        eventDocs.AddRange(
            requestDocResult.Documents
                .Where(d => d.DocumentId != null)
                .Select(d => new DocumentLink
                {
                    EntityType = "request",
                    EntityId = id,
                    DocumentId = d.DocumentId!.Value,
                    IsUnlink = true
                })
        );

        if (eventDocs.Any())
        {
            var integrationEvent = new DocumentLinkedIntegrationEvent
            {
                SessionId = sessionId,
                DocumentLinks = eventDocs
            };
            await bus.Publish(integrationEvent, cancellationToken);
        }

        foreach (var rd in requestDocResult.Documents)
        {
            var removeDocCommand = rd.Adapt<RemoveRequestDocumentCommand>();
            await sender.Send(removeDocCommand, cancellationToken);
        }

        return new DeleteRequestResult(result.IsSuccess);
    }

    public async Task<UpdateRequestResult> UpdateRequestAsync(RequestDto request, ISender sender,
        CancellationToken cancellationToken)
    {
        // Sync Request Titles
        var requestTitleResult = await sender.Send(new SyncRequestTitlesCommand
        {
            SessionId = request.SessionId,
            RequestId = request.Id,
            RequestTitleDtos = request.Titles
        }, cancellationToken);

        // Update Request Comments
        var requestCommentResult = await sender.Send(new SyncRequestCommentsCommand
        {
            RequestId = request.Id,
            RequestCommentDtos = request.Comments
        }, cancellationToken);

        // Update Request
        var requestUpdateCommand = request.Adapt<UpdateRequestCommand>();
        var requestUpdateResult = await sender.Send(requestUpdateCommand, cancellationToken);

        //Query Existing Request Documents
        var requestDocCommand = new GetRequestDocumentQuery(request.Id);
        var requestDocResult = await sender.Send(requestDocCommand, cancellationToken);
        var existingRequestDocs = requestDocResult.Documents;

        // Check New Documents
        var newDocuments = request.Documents
            .Where(i => !existingRequestDocs.Any(e => e.Id == i.Id))
            .ToList();

        // Documents will be Remove in Table
        var removeDocuments = existingRequestDocs
            .Where(e => !request.Documents.Any(i => i.Id == e.Id))
            .ToList();

        // Documents will be Update in Table
        var updateDocuments = request.Documents
            .Where(i => existingRequestDocs.Any(e => e.Id == i.Id && (
                e.DocumentId != i.DocumentId ||
                e.FileName != i.FileName ||
                e.Prefix != i.Prefix ||
                e.Set != i.Set ||
                e.FilePath != i.FilePath ||
                e.DocumentFollowUp != i.DocumentFollowUp ||
                e.DocumentClassification != i.DocumentClassification.ToDomain() ||
                e.DocumentDescription != i.DocumentDescription ||
                !e.UploadInfo.Equals(i.UploadInfo.ToDomain())
            )))
            .ToList();

        var updatedNewDocumentId = request.Documents
            .Where(i => existingRequestDocs.Any(e =>
                e.Id == i.Id && e.DocumentId != i.DocumentId && i.DocumentId != null))
            .ToList();

        var updatedOldDocumentId = existingRequestDocs
            .Where(e => request.Documents.Any(i =>
                e.Id == i.Id && e.DocumentId != i.DocumentId && e.DocumentId != null))
            .ToList();

        // List Documents for Publish Event
        var eventDocs = new List<DocumentLink>();
        eventDocs.AddRange(
            newDocuments.Where(i => i.DocumentId != null).Select(d => new DocumentLink
            {
                EntityType = "request",
                EntityId = request.Id,
                DocumentId = d.DocumentId!.Value,
                IsUnlink = false
            })
        );

        eventDocs.AddRange(
            updatedNewDocumentId.Where(i => i.DocumentId != null).Select(d => new DocumentLink
            {
                EntityType = "request",
                EntityId = request.Id,
                DocumentId = d.DocumentId!.Value,
                IsUnlink = false
            })
        );

        eventDocs.AddRange(
            removeDocuments.Where(i => i.DocumentId != null).Select(d => new DocumentLink
            {
                EntityType = "request",
                EntityId = request.Id,
                DocumentId = d.DocumentId!.Value,
                IsUnlink = true
            })
        );

        eventDocs.AddRange(
            updatedOldDocumentId.Where(i => i.DocumentId != null).Select(d => new DocumentLink
            {
                EntityType = "request",
                EntityId = request.Id,
                DocumentId = d.DocumentId!.Value, // Wrong because Tracking entities has updated new DocumentId. This makes new DocumentId always be Unlink
                IsUnlink = true
            })
        );

        if (eventDocs.Any())
        {
            var integrationEvent = new DocumentLinkedIntegrationEvent
            {
                SessionId = request.SessionId,
                DocumentLinks = eventDocs
            };
            await bus.Publish(integrationEvent, cancellationToken);
        }

        // Add New Documents
        var newDocCommand = new AddRequestDocumentCommand(request.Id, newDocuments);
        await sender.Send(newDocCommand, cancellationToken);

        // Update Documents
        var updateDocCommand = new UpdateRequestDocumentCommand(request.Id, updateDocuments);
        await sender.Send(updateDocCommand, cancellationToken);

        // Remove Documents
        foreach (var rd in removeDocuments)
        {
            var removeDocCommand = rd.Adapt<RemoveRequestDocumentCommand>();
            await sender.Send(removeDocCommand, cancellationToken);
        }

        return new UpdateRequestResult(requestUpdateResult.IsSuccess);
    }

    public async Task<UpdateDraftRequestResult> UpdateRequestDraftAsync(RequestDto request, ISender sender,
        CancellationToken cancellationToken)
    {
        // Update Request Titles
        var requestTitleResult = await sender.Send(new SyncDraftRequestTitlesCommand
        {
            SessionId = request.SessionId,
            RequestId = request.Id,
            RequestTitleDtos = request.Titles
        }, cancellationToken);

        // Update Request Comments
        var requestCommentResult = await sender.Send(new SyncRequestCommentsCommand
        {
            RequestId = request.Id,
            RequestCommentDtos = request.Comments
        }, cancellationToken);

        // Update Request
        var requestUpdateCommand = request.Adapt<UpdateDraftRequestCommand>();
        var requestUpdateResult = await sender.Send(requestUpdateCommand, cancellationToken);

        //Query Existing Request Documents
        var requestDocCommand = new GetRequestDocumentQuery(request.Id);
        var requestDocResult = await sender.Send(requestDocCommand);
        var existingRequestDocs = requestDocResult.Documents;

        // Check New Documents
        var newDocuments = request.Documents
            .Where(i => !existingRequestDocs.Any(e => e.Id == i.Id))
            .ToList();

        // Documents will be Remove in Table
        var removeDocuments = existingRequestDocs
            .Where(e => !request.Documents.Any(i => i.Id == e.Id))
            .ToList();

        // Documents will be Update in Table
        var updateDocuments = request.Documents
            .Where(i => existingRequestDocs.Any(e => e.Id == i.Id && (
                e.DocumentId != i.DocumentId ||
                e.FileName != i.FileName ||
                e.Prefix != i.Prefix ||
                e.Set != i.Set ||
                e.FilePath != i.FilePath ||
                e.DocumentFollowUp != i.DocumentFollowUp ||
                e.DocumentClassification != i.DocumentClassification.ToDomain() ||
                e.DocumentDescription != i.DocumentDescription ||
                !e.UploadInfo.Equals(i.UploadInfo.ToDomain())
            )))
            .ToList();

        var updatedNewDocumentId = request.Documents
            .Where(i => existingRequestDocs.Any(e =>
                e.Id == i.Id && e.DocumentId != i.DocumentId && i.DocumentId != null))
            .ToList();

        var updatedOldDocumentId = existingRequestDocs
            .Where(e => request.Documents.Any(i =>
                e.Id == i.Id && e.DocumentId != i.DocumentId && e.DocumentId != null))
            .ToList();


        // Add New Documents
        var newDocCommand = new AddRequestDocumentCommand(request.Id, newDocuments);
        await sender.Send(newDocCommand, cancellationToken);

        // Update Documents
        var updateDocCommand = new UpdateRequestDocumentCommand(request.Id, updateDocuments);
        await sender.Send(updateDocCommand, cancellationToken);

        // Remove Documents
        foreach (var rd in removeDocuments)
        {
            var removeDocCommand = rd.Adapt<RemoveRequestDocumentCommand>();
            await sender.Send(removeDocCommand, cancellationToken);
        }

        // List Documents for Publish Event
        var eventDocs = new List<DocumentLink>();
        eventDocs.AddRange(
            newDocuments.Where(i => i.DocumentId != null).Select(d => new DocumentLink
            {
                EntityType = "request",
                EntityId = request.Id,
                DocumentId = d.DocumentId!.Value,
                IsUnlink = false
            })
        );

        eventDocs.AddRange(
            updatedNewDocumentId.Where(i => i.DocumentId != null).Select(d => new DocumentLink
            {
                EntityType = "request",
                EntityId = request.Id,
                DocumentId = d.DocumentId!.Value,
                IsUnlink = false
            })
        );

        eventDocs.AddRange(
            removeDocuments.Where(i => i.DocumentId != null).Select(d => new DocumentLink
            {
                EntityType = "request",
                EntityId = request.Id,
                DocumentId = d.DocumentId!.Value,
                IsUnlink = true
            })
        );

        eventDocs.AddRange(
            updatedOldDocumentId.Where(i => i.DocumentId != null).Select(d => new DocumentLink
            {
                EntityType = "request",
                EntityId = request.Id,
                DocumentId = d.DocumentId!.Value,
                IsUnlink = true
            })
        );

        if (eventDocs.Any())
        {
            var integrationEvent = new DocumentLinkedIntegrationEvent
            {
                SessionId = request.SessionId,
                DocumentLinks = eventDocs
            };
            await bus.Publish(integrationEvent, cancellationToken);
        }

        return new UpdateDraftRequestResult(requestUpdateResult.IsSuccess);
    }
}