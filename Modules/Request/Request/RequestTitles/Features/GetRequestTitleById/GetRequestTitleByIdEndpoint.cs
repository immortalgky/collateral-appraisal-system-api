namespace Request.RequestTitles.Features.GetRequestTitleById;

public class GetRequestTitleByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/requests/{requestId:long}/titles/{titleId:long}",
            async (long requestId, long titleId, ISender sender, CancellationToken cancellationToken) =>
            {
                var query = new GetRequestTitleByIdQuery(requestId, titleId);

                var result = await sender.Send(query, cancellationToken);

                var response = result.Adapt<GetRequestTitleByIdResponse>();

                return Results.Ok(response);
            })
            .WithName("GetRequestTitleById")
            .Produces<GetRequestTitleByIdResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get request title by ID")
            .WithDescription("Retrieves a specific title/collateral by its ID for the specified request. Returns detailed information about the title including land area, building, vehicle, and machine details.")
            .WithTags("Request Titles");
    }
}