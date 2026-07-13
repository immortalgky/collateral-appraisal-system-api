namespace Parameter.Dealers.Features.GetDealers;

public class GetDealersEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/dealers",
                async (ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new GetDealersQuery(), cancellationToken);
                    return Results.Ok(result.Dealers);
                })
            .WithName("GetDealers")
            .Produces<List<DealerDto>>(StatusCodes.Status200OK)
            .WithSummary("Get dealers")
            .WithDescription("Retrieve all dealers (dealer code + name) for the request Dealer Code dropdown.")
            .WithTags("Parameter");
    }
}
