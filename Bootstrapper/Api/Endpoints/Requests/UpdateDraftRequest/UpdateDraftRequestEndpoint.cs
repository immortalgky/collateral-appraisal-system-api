using Mapster;
using MediatR;
using Request.Application.Features.Requests.UpdateDraftRequest;

namespace Api.Endpoints.Requests.UpdateDraftRequest;

public class UpdateDraftRequestEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch("/requests/{id:guid}/draft",
                async (Guid id, UpdateDraftRequestRequest request, ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var command = request.Adapt<UpdateDraftRequestCommand>() with { Id = id };
                    var result = await sender.Send(command, cancellationToken);
                    return Results.Ok(result.IsSuccess);
                })
            .WithName("UpdateDraftRequest")
            .Produces<UpdateDraftRequestResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("Requests")
            .WithSummary("Update a draft request")
            .WithDescription("Updates an existing draft request.")
            .AllowAnonymous();
    }
}