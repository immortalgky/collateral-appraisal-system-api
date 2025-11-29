using Request.RequestTitles.Features.SyncDraftRequestTitles;

namespace Request.RequestTitles.Features.UpdateDraftRequestTitle;

public class UpdateDraftRequestTitleEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch("/requests/{requestId:guid}/titles/draft", 
            async (Guid requestId, ISender sender, UpdateDraftRequestTitleRequest request, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new SyncDraftRequestTitleCommand(Guid.NewGuid(), requestId, request.RequestTitleDtos), cancellationToken);

                var response = new UpdateDraftRequestTitleResponse(result.RequestTitles);

                return Results.Ok(response);
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
