using Request.Services;

namespace Request.Requests.Features.DeleteRequest;

public class DeleteRequestEndpoint(IRequestService requestService) : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/requests/{id}",
                async (Guid id, Guid sessionId, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result =
                        await requestService.DeleteRequestAsync(id, sessionId, sender, cancellationToken);

                    var response = result.Adapt<DeleteRequestResponse>();

                    return Results.Ok(response);
                })
            .WithName("DeleteRequest")
            .Produces<DeleteRequestResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Delete request by ID")
            .WithDescription(
                "Deletes a request by its ID. If the request does not exist, a 404 Not Found error is returned.")
            .AllowAnonymous();
        // .RequireAuthorization("CanWriteRequest");
    }
}