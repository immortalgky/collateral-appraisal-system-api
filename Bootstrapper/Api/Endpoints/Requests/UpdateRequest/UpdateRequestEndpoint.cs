using Mapster;
using MediatR;
using Request.Application.Features.Requests.UpdateRequest;

namespace Api.Endpoints.Requests.UpdateRequest;

public class UpdateRequestEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/requests/{id:guid}",
                async (Guid id, UpdateRequestRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = request.Adapt<UpdateRequestCommand>() with { Id = id };
                    var result = await sender.Send(command, cancellationToken);
                    return Results.Ok(result.IsSuccess);
                })
            .WithName("UpdateRequest")
            .Produces<UpdateRequestResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("Requests")
            .WithSummary("Update an existing request")
            .WithDescription("Updates an existing request in the system.")
            .AllowAnonymous();
    }
}