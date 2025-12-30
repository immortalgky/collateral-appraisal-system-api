using Carter;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Request.Application.Features.RequestTitles.GetRequestTitleById;

namespace Api.Endpoints.RequestTitles.GetRequestTitleById;

public class GetRequestTitleByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/requests/{requestId:guid}/titles/{titleId:guid}",
                async (Guid requestId, Guid titleId, ISender sender, CancellationToken cancellationToken) =>
                {
                    var query = new GetRequestTitleByIdQuery(requestId, titleId);
                    var result = await sender.Send(query, cancellationToken);
                    var response = result.Adapt<GetRequestTitleByIdResponse>();
                    return Results.Ok(response);
                })
            .WithName("GetRequestTitleById")
            .Produces<GetRequestTitleByIdResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("Request Titles")
            .WithSummary("Get request title by ID")
            .WithDescription("Retrieves a specific title/collateral by its ID for the specified request.")
            .AllowAnonymous();
    }
}
