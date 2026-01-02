using Mapster;
using MediatR;
using Request.Application.Features.Requests.GetRequestById;

namespace Api.Endpoints.Requests.GetRequestById;

public class GetRequestByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/requests/{id:guid}",
                async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new GetRequestByIdQuery(id), cancellationToken);
                    var response = result.Adapt<GetRequestByIdResult>();
                    return Results.Ok(response);
                })
            .WithName("GetRequestById")
            .Produces<GetRequestByIdResult>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("Requests")
            .WithSummary("Get request by ID")
            .WithDescription("Retrieves a specific request by its unique identifier.")
            .AllowAnonymous();
    }
}