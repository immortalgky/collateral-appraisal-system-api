namespace Request.Requests.Features.DeleteRequest;

public class DeleteRequestEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/requests/{id:Guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new DeleteRequestCommand(id), cancellationToken);

                var response = result.Adapt<DeleteRequestResponse>();

                return Results.Ok(response);
            })
            .WithName("DeleteRequest")
            .Produces<DeleteRequestResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Delete request by ID")
            .WithDescription(
                "Deletes a request by its ID. If the request does not exist, a 404 Not Found error is returned.")
            .RequireAuthorization("CanWriteRequest");
    }
}