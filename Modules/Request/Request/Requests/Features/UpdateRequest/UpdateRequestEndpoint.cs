namespace Request.Requests.Features.UpdateRequest;

public class UpdateRequestEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/requests/{id:long}",
                async (long id, UpdateRequestRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = request.Adapt<UpdateRequestCommand>() with { Id = id };

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<UpdateRequestResponse>();

                    return Results.Ok(response);
                })
            .WithName("UpdateRequest")
            .Produces<UpdateRequestResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update an existing request")
            .WithDescription(
                "Updates an existing request in the system. The request details are provided in the request body.");
    }
}