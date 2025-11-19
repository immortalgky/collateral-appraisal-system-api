namespace Request.RequestTitles.Features.DraftRequestTitle;

public class DraftRequestTitleEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/requests/{requestId:Guid}/titles/draft", 
                async (Guid requestId, DraftRequestTitleRequest request, IRequestTitleService requestTitleService, ISender sender, CancellationToken cancellationToken) =>
                {
                    await requestTitleService.DraftRequestTitlesAsync(requestId, request.RequestTitleDtos, cancellationToken);
                    
                    var response = new CreateRequestTitleResponse(true);

                    return Results.Created($"/requests/{requestId}", response);
                })
            .WithName("DraftRequestTitle")
            .Produces<DraftRequestTitleResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Draft a new request title")
            .WithDescription(
                "Draft a new title/collateral for the specified request. The title details including land area, building information, vehicle, and machine details are provided in the request body.")
            .WithTags("Request Titles")
            .AllowAnonymous();
            // .RequireAuthorization("CanWriteRequest");
  }
}
