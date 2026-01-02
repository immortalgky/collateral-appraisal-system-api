using MediatR;
using Request.Application.Features.Requests.DeleteRequest;

namespace Api.Endpoints.Requests.DeleteRequest;

public class DeleteRequestEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/requests/{id:guid}",
                async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new DeleteRequestCommand(id);
                    var result = await sender.Send(command, cancellationToken);
                    return Results.Ok(result.IsSuccess);
                })
            .WithName("DeleteRequest")
            .Produces<DeleteRequestResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("Requests")
            .WithSummary("Delete request by ID")
            .WithDescription("Deletes a request by its ID.")
            .AllowAnonymous();
    }
}