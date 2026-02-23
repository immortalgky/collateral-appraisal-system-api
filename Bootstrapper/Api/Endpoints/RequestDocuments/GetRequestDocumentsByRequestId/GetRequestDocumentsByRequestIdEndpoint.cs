using Carter;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Request.Application.Features.RequestDocuments.GetRequestDocumentsByRequestId;

namespace Api.Endpoints.RequestDocuments.GetRequestDocumentsByRequestId;

public class GetRequestDocumentsByRequestIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/requests/{requestId:guid}/documents",
                async (Guid requestId, ISender sender, CancellationToken cancellationToken) =>
                {
                    var query = new GetRequestDocumentsByRequestIdQuery(requestId);
                    var result = await sender.Send(query, cancellationToken);
                    var response = result.Adapt<GetRequestDocumentsByRequestIdResponse>();
                    return Results.Ok(response);
                })
            .WithName("GetRequestDocumentsByRequestId")
            .Produces<GetRequestDocumentsByRequestIdResponse>(StatusCodes.Status200OK)
            .WithTags("Request Documents")
            .WithSummary("Get all documents for a request")
            .WithDescription("Retrieves all documents for a request, grouped by request-level and per-title sections.")
            .AllowAnonymous();
    }
}
