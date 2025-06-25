using UpdatePropertyRequest = System.Collections.Generic.List<Request.Contracts.Requests.Dtos.PropertyDto>;

namespace Request.Requests.Features.UpdateProperty;

public record UpdatePropertyResponse(bool IsSuccess);

public class UpdatePropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/requests/{id}/updateproperty", async (long id, UpdatePropertyRequest property, ISender sender) =>
        {
            var command = new UpdatePropertyCommand(id, property);
            var result = await sender.Send(command);
            var response = result.Adapt<UpdatePropertyResponse>();

            return Results.Ok(response);
        })
        .WithName("UpdateProperty")
        .Produces<UpdatePropertyResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Update an property")
        .WithDescription(
            "Update list a new property in the system");
    }
}