using MassTransit;
using Request.Contracts.RequestDocuments.Dto;
using Request.Extensions;
using Request.RequestDocuments.Features.AddRequestDocument;
using Request.Requests.Features.CreateRequest;
using Shared.Messaging.Events;
using Request.Requests.Features.UpdateRequest;
using Request.Requests.Features.CreateDraftRequest;
using Request.Requests.Features.UpdateDraftRequest;
using Request.RequestDocuments;
using Request.RequestDocuments.Features.GetRequestDocument;
using Request.RequestDocuments.Features.RemoveRequestDocument;


namespace Request.Services;

public class RequestService(IBus bus) : IRequestService
{
    public async Task<CreateRequestResult> CreateRequestAsync(
        RequestDto request, ISender sender,
        CancellationToken cancellation)
    {
        var requestCommand = request.Adapt<CreateRequestCommand>();
        var requestResult = await sender.Send(requestCommand, cancellation);

        var requestDoc = new AddRequestDocumentCommand(requestResult.Id, request.Documents);
        await sender.Send(requestDoc, cancellation);


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

        await bus.Publish(integrationEvent, cancellation);


        return new CreateRequestResult(requestResult.Id);
    }

    public async Task<CreateDraftRequestResult> CreateRequestDraftAsync(RequestDto request, ISender sender,
        CancellationToken cancellation)
    {
        var requestCommand = request.Adapt<CreateDraftRequestCommand>();
        var requestResult = await sender.Send(requestCommand, cancellation);

        var requestDoc = new AddRequestDocumentCommand(requestResult.Id, request.Documents);
        await sender.Send(requestDoc, cancellation);


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

        await bus.Publish(integrationEvent, cancellation);


        return new CreateDraftRequestResult(requestResult.Id);
    }

    public async Task<UpdateRequestResult> UpdateRequestAsync(RequestDto request, ISender sender,
        CancellationToken cancellation)
    {
        var requestUpdateCommand = request.Adapt<UpdateRequestCommand>();
        var requestUpdateResult = await sender.Send(requestUpdateCommand, cancellation);

        var requestDoc = new GetRequestDocumentQuery(request.Id);
        var requestDocResult = await sender.Send(requestDoc);

        var documents = request.Documents
            .Select(d => RequestDocument.Create(
                requestId: request.Id,
                documentId: d.DocumentId,
                documentClassification: d.DocumentClassification.ToDomain(),
                documentDescription: d.DocumentDescription,
                uploadInfo: d.UploadInfo.ToDomain()
            ))
            .ToList();

        var newDoc = documents
            .Where(i => !requestDocResult.Documents.Any(e => e.DocumentId == i.DocumentId))
            .ToList();

        var removeDoc = requestDocResult.Documents
            .Where(e => !documents.Any(i => i.DocumentId == e.DocumentId))
            .ToList();

        var newDocCommand = new AddRequestDocumentCommand(request.Id, newDoc.Adapt<List<RequestDocumentDto>>());
        await sender.Send(newDocCommand, cancellation);

        foreach (var rd in removeDoc)
        {
            var removeDocCommand = rd.Adapt<RemoveRequestDocumentCommand>();
            await sender.Send(removeDocCommand, cancellation);
        }

        var eventDocs = new List<DocumentLink>();
        eventDocs.AddRange(
            newDoc.Select(d => new DocumentLink
            {
                EntityType = "request",
                EntityId = request.Id,
                DocumentId = d.DocumentId,
                IsUnlink = false
            })
        );

        eventDocs.AddRange(
            removeDoc.Select(d => new DocumentLink
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
            await bus.Publish(integrationEvent, cancellation);
        }

        return new UpdateRequestResult(requestUpdateResult.IsSuccess);
    }

    public async Task<UpdateDraftRequestResult> UpdateRequestDraftAsync(RequestDto request, ISender sender,
        CancellationToken cancellation)
    {
        var requestUpdateCommand = request.Adapt<UpdateDraftRequestCommand>();
        var requestUpdateResult = await sender.Send(requestUpdateCommand, cancellation);

        var requestDoc = new GetRequestDocumentQuery(request.Id);
        var requestDocResult = await sender.Send(requestDoc);

        var documents = request.Documents
            .Select(d => RequestDocument.Create(
                requestId: request.Id,
                documentId: d.DocumentId,
                documentClassification: d.DocumentClassification.ToDomain(),
                documentDescription: d.DocumentDescription,
                uploadInfo: d.UploadInfo.ToDomain()
            ))
            .ToList();

        var newDoc = documents
            .Where(i => !requestDocResult.Documents.Any(e => e.DocumentId == i.DocumentId))
            .ToList();

        var removeDoc = requestDocResult.Documents
            .Where(e => !documents.Any(i => i.DocumentId == e.DocumentId))
            .ToList();

        var newDocCommand = new AddRequestDocumentCommand(request.Id, newDoc.Adapt<List<RequestDocumentDto>>());
        await sender.Send(newDocCommand, cancellation);

        foreach (var rd in removeDoc)
        {
            var removeDocCommand = rd.Adapt<RemoveRequestDocumentCommand>();
            await sender.Send(removeDocCommand, cancellation);
        }

        var eventDocs = new List<DocumentLink>();
        eventDocs.AddRange(
            newDoc.Select(d => new DocumentLink
            {
                EntityType = "request",
                EntityId = request.Id,
                DocumentId = d.DocumentId,
                IsUnlink = false
            })
        );

        eventDocs.AddRange(
            removeDoc.Select(d => new DocumentLink
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
            await bus.Publish(integrationEvent, cancellation);
        }


        return new UpdateDraftRequestResult(requestUpdateResult.IsSuccess);
    }
}
