using Carter;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Request.Application.Features.RequestTitles.GetRequestTitlesByRequestId;

namespace Api.Endpoints.RequestTitles.GetRequestTitlesByRequestId;

public class GetRequestTitlesByRequestIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/requests/{requestId:guid}/titles",
                async (Guid requestId, ISender sender, CancellationToken cancellationToken) =>
                {
                    var query = new GetRequestTitlesByRequestIdQuery(requestId);
                    var result = await sender.Send(query, cancellationToken);
                    var response = result.Adapt<GetRequestTitlesByRequestIdResponse>();
                    return Results.Ok(response);
                })
            .WithName("GetRequestTitlesByRequestId")
            .Produces<GetRequestTitlesByRequestIdResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("Request Titles")
            .WithSummary("Get all titles for a request")
            .WithDescription("Retrieves all titles/collaterals associated with the specified request.")
            .AllowAnonymous();
    }
}
