using MassTransit;
using Request.RequestTitles.Features.GetLinkRequestTitleDocumentById;
using Shared.Messaging.Events;

namespace Request.RequestTitles.Features.RemoveLinkRequestTitleDocument;

public class RemoveLinkRequestTitleDocumentByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/requests/{requestId:Guid}/titles/{titleId:Guid}/titleDocs/{titleDocId:Guid}",
            async (Guid titleId, Guid titleDocId, ISender sender, IBus bus, CancellationToken cancellationToken) =>
            {
                var requestTitle = await sender.Send(new GetLinkRequestTitleDocumentByIdQuery(titleDocId), cancellationToken);
                var result = await sender.Send(new RemoveLinkRequestTitleDocumentByIdCommand(titleDocId, titleId), cancellationToken);

                var reponse = new RemoveLinkRequestTitleDocumentByIdResponse(
                    true);

                if (requestTitle.DocumentId != Guid.Empty)
                {
                    await bus.Publish(
                        new DocumentLinkedIntegrationEvent
                        {
                            SessionId = Guid.NewGuid(),
                            DocumentLinks = new List<DocumentLink>() { new DocumentLink
                            {
                                EntityType = "Title",
                                EntityId = titleId,
                                DocumentId = requestTitle.DocumentId!.Value,
                                IsUnlinked = true
                            } }
                        }, 
                        cancellationToken
                        );
                }
                    
                return Results.Ok(reponse);
            })
            .WithName("RemoveLinkRequestTitleDocumentById")
            .Produces<RemoveLinkRequestTitleDocumentByIdResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("remove request title document")
            .WithDescription(
                "")
            .WithTags("Request Titles")
            .AllowAnonymous();
        // .RequireAuthorization("CanReadRequest")
    }
}