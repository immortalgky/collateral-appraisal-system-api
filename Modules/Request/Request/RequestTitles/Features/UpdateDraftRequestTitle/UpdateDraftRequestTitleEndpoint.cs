namespace Request.RequestTitles.Features.UpdateDraftRequestTitle;

public class UpdateDraftRequestTitleEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch("/requests/{requestId:guid}/titles/draft", 
            async (Guid requestId, IRequestTitleService requestTitleService, UpdateDraftRequestTitleRequest request, CancellationToken cancellationToken) =>
            {
                await requestTitleService.UpdateDraftRequestTitlesAsync(Guid.NewGuid(), requestId, request.RequestTitleDtos, cancellationToken);
                
                return Results.Ok(true);
            })
            .WithName("UpdateDraftRequestTitle")
            .Produces<UpdateDraftRequestTitleResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update draft a request title")
            .WithDescription(
                "Updates draft an existing title/collateral for the specified request. All title details including land area, building information, vehicle, and machine details can be modified.")
            .WithTags("Request Titles")
            .AllowAnonymous();
        // .RequireAuthorization("CanWriteRequest");
    }
}
