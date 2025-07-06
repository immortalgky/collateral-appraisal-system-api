namespace Request.Requests.Features.GetRequestById;

public class GetRequestByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/requests/{id:long}", async (long id, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetRequestByIdQuery(id), cancellationToken);

                var response = result.Adapt<GetRequestByIdResponse>();

                return Results.Ok(response);
            })
            .WithName("GetRequestById")
            .Produces<GetRequestByIdResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get request by ID")
            .WithDescription("Get request by ID");
    }
}