using MassTransit;
using Request.Contracts.RequestDocuments.Dto;
using Request.Extensions;
using Request.RequestDocuments.Features.AddRequestDocument;
using Request.Requests.Features.CreateRequest;
using Shared.Messaging.Events;
using Request.Requests.Features.UpdateRequest;
using Request.Requests.Features.CreateDraftRequest;
using Request.Requests.Features.UpdateDraftRequest;
using Request.RequestDocuments.Features.GetRequestDocument;
using Request.RequestDocuments.Features.RemoveRequestDocument;
using Request.Requests.Features.DeleteRequest;


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
        /*
            var requestTitleCommand = request.Title.Adapt<AddRequestTitleCommand>();
            var requestResult = await sender.Send(requestTitleCommand, cancellationToken);

            var requestTitleDocCommand = new AddRequestTitleDoc();
        */

        // IntegrationEvent
        var integrationEvent = new DocumentLinkedIntegrationEvent
        {
            SessionId = request.SessionId,
            Documents = request.Documents
                .Select(d => new DocumentLink
                {
                    EntityType = "request",
                    EntityId = requestResult.Id,
                    DocumentId = d.DocumentId
                })
                .ToList()
        };
        await bus.Publish(integrationEvent, cancellationToken);

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
                .Select(d => new DocumentLink
                {
                    EntityType = "request",
                    EntityId = requestResult.Id,
                    DocumentId = d.DocumentId,
                    IsUnlink = false
                })
                .ToList()
        };
        await bus.Publish(integrationEvent, cancellationToken);

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
            requestDocResult.Documents.Select(d => new DocumentLink
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

        // Documents From Front End
        var documents = request.Documents
            .Select(d => RequestDocument.Create(
                requestId: request.Id,
                documentId: d.DocumentId,
                documentClassification: d.DocumentClassification.ToDomain(),
                documentDescription: d.DocumentDescription,
                uploadInfo: d.UploadInfo.ToDomain()
            ))
            .ToList();

        // Check New Documents
        var newDocuments = documents
            .Where(i => !requestDocResult.Documents.Any(e => e.DocumentId == i.DocumentId))
            .ToList();

        // Documents will be Remove in Table
        var removeDocuments = requestDocResult.Documents
            .Where(e => !documents.Any(i => i.DocumentId == e.DocumentId))
            .ToList();

        // Add New Documents
        var newDocCommand = new AddRequestDocumentCommand(request.Id, newDocuments.Adapt<List<RequestDocumentDto>>());
        await sender.Send(newDocCommand, cancellationToken);

        // Remove Documents
        foreach (var rd in removeDocuments)
        {
            var removeDocCommand = rd.Adapt<RemoveRequestDocumentCommand>();
            await sender.Send(removeDocCommand, cancellationToken);
        }

        // List Documents for Publish Event
        var eventDocs = new List<DocumentLink>();
        eventDocs.AddRange(
            newDocuments.Select(d => new DocumentLink
            {
                EntityType = "request",
                EntityId = request.Id,
                DocumentId = d.DocumentId,
                IsUnlink = false
            })
        );

        eventDocs.AddRange(
            removeDocuments.Select(d => new DocumentLink
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

        // Documents From Front End
        var documents = request.Documents
            .Select(d => RequestDocument.Create(
                requestId: request.Id,
                documentId: d.DocumentId,
                documentClassification: d.DocumentClassification.ToDomain(),
                documentDescription: d.DocumentDescription,
                uploadInfo: d.UploadInfo.ToDomain()
            ))
            .ToList();

        // Check New Documents
        var newDocuments = documents
            .Where(i => !requestDocResult.Documents.Any(e => e.DocumentId == i.DocumentId))
            .ToList();

        // Documents will be Remove in Table
        var removeDocuments = requestDocResult.Documents
            .Where(e => !documents.Any(i => i.DocumentId == e.DocumentId))
            .ToList();

        // Add New Documents
        var newDocCommand = new AddRequestDocumentCommand(request.Id, newDocuments.Adapt<List<RequestDocumentDto>>());
        await sender.Send(newDocCommand, cancellationToken);

        // Remove Documents
        foreach (var rd in removeDocuments)
        {
            var removeDocCommand = rd.Adapt<RemoveRequestDocumentCommand>();
            await sender.Send(removeDocCommand, cancellationToken);
        }

        // List Documents for Publish Event
        var eventDocs = new List<DocumentLink>();
        eventDocs.AddRange(
            newDocuments.Select(d => new DocumentLink
            {
                EntityType = "request",
                EntityId = request.Id,
                DocumentId = d.DocumentId,
                IsUnlink = false
            })
        );

        eventDocs.AddRange(
            removeDocuments.Select(d => new DocumentLink
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