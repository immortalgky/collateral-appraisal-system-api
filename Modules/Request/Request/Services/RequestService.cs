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
using Request.Extensions;
using System.Text.Json;

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

        // IntegrationEvent
        var integrationEvent = new DocumentLinkedIntegrationEvent
        {
            SessionId = request.SessionId,
            Documents = request.Documents
                .Where(d => d.DocumentId != null)
                .Select(d => new DocumentLink
                {
                    EntityType = "request",
                    EntityId = requestResult.Id,
                    DocumentId = d.DocumentId
                })
                .ToList()
        };

        if (integrationEvent.Documents.Any())
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

        // Add Request Documents
        var requestDocCommand = new AddRequestDocumentCommand(requestResult.Id, request.Documents);
        await sender.Send(requestDocCommand, cancellationToken);


        // IntregationEvent
        var integrationEvent = new DocumentLinkedIntegrationEvent
        {
            SessionId = request.SessionId,
            Documents = request.Documents
                .Where(d => d.DocumentId != null)
                .Select(d => new DocumentLink
                {
                    EntityType = "request",
                    EntityId = requestResult.Id,
                    DocumentId = d.DocumentId,
                    IsUnlink = false
                })
                .ToList()
        };
        if (integrationEvent.Documents.Any())
        {
            await bus.Publish(integrationEvent, cancellationToken);
        }

        return new CreateDraftRequestResult(requestResult.Id);
    }

    // Delete Request
    public async Task<DeleteRequestResult> DeleteRequestAsync(Guid id, Guid sessionId, ISender sender,
        CancellationToken cancellationToken)
    {
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
                    DocumentId = d.DocumentId,
                    IsUnlink = true
                })
        );

        if (eventDocs.Any())
        {
            var integrationEvent = new DocumentLinkedIntegrationEvent
            {
                SessionId = sessionId,
                Documents = eventDocs
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
                DocumentId = d.DocumentId,
                IsUnlink = false
            })
        );

        eventDocs.AddRange(
            updatedNewDocumentId.Where(i => i.DocumentId != null).Select(d => new DocumentLink
            {
                EntityType = "request",
                EntityId = request.Id,
                DocumentId = d.DocumentId,
                IsUnlink = false
            })
        );

        eventDocs.AddRange(
            removeDocuments.Where(i => i.DocumentId != null).Select(d => new DocumentLink
            {
                EntityType = "request",
                EntityId = request.Id,
                DocumentId = d.DocumentId,
                IsUnlink = true
            })
        );

        eventDocs.AddRange(
            updatedOldDocumentId.Where(i => i.DocumentId != null).Select(d => new DocumentLink
            {
                EntityType = "request",
                EntityId = request.Id,
                DocumentId = d.DocumentId,
                IsUnlink = true
            })
        );

        if (eventDocs.Any())
        {
            var integrationEvent = new DocumentLinkedIntegrationEvent
            {
                SessionId = request.SessionId,
                Documents = eventDocs
            };
            await bus.Publish(integrationEvent, cancellationToken);
        }

        return new UpdateRequestResult(requestUpdateResult.IsSuccess);
    }

    public async Task<UpdateDraftRequestResult> UpdateRequestDraftAsync(RequestDto request, ISender sender,
        CancellationToken cancellationToken)
    {
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
                DocumentId = d.DocumentId,
                IsUnlink = false
            })
        );

        eventDocs.AddRange(
            updatedNewDocumentId.Where(i => i.DocumentId != null).Select(d => new DocumentLink
            {
                EntityType = "request",
                EntityId = request.Id,
                DocumentId = d.DocumentId,
                IsUnlink = false
            })
        );

        eventDocs.AddRange(
            removeDocuments.Where(i => i.DocumentId != null).Select(d => new DocumentLink
            {
                EntityType = "request",
                EntityId = request.Id,
                DocumentId = d.DocumentId,
                IsUnlink = true
            })
        );

        eventDocs.AddRange(
            updatedOldDocumentId.Where(i => i.DocumentId != null).Select(d => new DocumentLink
            {
                EntityType = "request",
                EntityId = request.Id,
                DocumentId = d.DocumentId,
                IsUnlink = true
            })
        );

        if (eventDocs.Any())
        {
            var integrationEvent = new DocumentLinkedIntegrationEvent
            {
                SessionId = request.SessionId,
                Documents = eventDocs
            };
            await bus.Publish(integrationEvent, cancellationToken);
        }

        return new UpdateDraftRequestResult(requestUpdateResult.IsSuccess);
    }
}