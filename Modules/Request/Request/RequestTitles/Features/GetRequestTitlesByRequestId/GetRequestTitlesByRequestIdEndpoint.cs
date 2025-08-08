namespace Request.RequestTitles.Features.GetRequestTitlesByRequestId;

public class GetRequestTitlesByRequestIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/requests/{requestId:long}/titles",
            async (long requestId, ISender sender, CancellationToken cancellationToken) =>
            {
                var query = new GetRequestTitlesByRequestIdQuery(requestId);

                var result = await sender.Send(query, cancellationToken);

                var response = result.Adapt<GetRequestTitlesByRequestIdResponse>();

                return Results.Ok(response);
            })
            .WithName("GetRequestTitlesByRequestId")
            .Produces<GetRequestTitlesByRequestIdResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get all titles for a request")
            .WithDescription("Retrieves all titles/collaterals associated with the specified request. Returns a list of titles with their detailed information including land area, building, vehicle, and machine details.")
            .WithTags("Request Titles");
    }
}