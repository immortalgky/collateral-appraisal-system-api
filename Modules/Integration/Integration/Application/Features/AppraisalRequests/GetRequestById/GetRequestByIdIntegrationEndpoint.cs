using Carter;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Request.Application.Features.Requests.GetRequestById;

namespace Integration.Application.Features.AppraisalRequests.GetRequestById;

// Mirrors legacy GET /requests/{id} for LOS (Bootstrapper/Api/Endpoints/Requests/GetRequestById/GetRequestByIdEndpoint.cs).
// Also resolves the Location header returned by POST /api/v1/requests.
public class GetRequestByIdIntegrationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/requests/{id:guid}",
                async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new GetRequestByIdQuery(id), cancellationToken);
                    var response = result.Adapt<GetRequestByIdResponse>();
                    return Results.Ok(response);
                })
            .WithName("Integration_GetRequestById")
            .WithTags("Integration - Requests")
            .Produces<GetRequestByIdResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization("Integration");
    }
}
