namespace Request.RequestTitles.Features.CreateRequestTitle;

public class CreateRequestTitlesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/requests/{requestId:Guid}/titles", async (Guid requestId, CreateRequestTitleRequest request,IRequestTitleService requestTitleService,CancellationToken cancellationToken) =>
            {
                await requestTitleService.CreateRequestTitlesAsync(Guid.NewGuid(),requestId, request.RequestTitleDtos, cancellationToken);

                var response = new CreateRequestTitleResponse(true);

                return Results.Created($"/requests/{requestId}", response);
            })
        .WithName("CreateRequestTitle")
        .Produces<CreateRequestTitleResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Create new request titles")
        .WithDescription(
            "Creates new titles/collaterals for the specified request. The title details including land area, building information, vehicle, and machine details are provided in the request body.")
        .WithTags("Request Titles")
        .AllowAnonymous();
        // .RequireAuthorization("CanWriteRequest");
    }
}