namespace Request.Requests.Features.GetRequests;

public class GetRequestEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/requests", async (ISender sender) =>
            {
                var result = await sender.Send(new GetRequestQuery());
                return Results.Ok(result);
            })
            .WithName("GetRequest")
            .Produces<GetRequestResult>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get all requests")
            .WithDescription(
                "Retrieves all requests from the system. This endpoint returns a list of requests with their details.");
    }
}