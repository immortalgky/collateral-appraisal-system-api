using Request.RequestTitles.Features.SyncRequestTitle;

namespace Request.RequestTitles.Features.UpdateRequestTitle;

public class UpdateRequestTitleEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch("/requests/{requestId:Guid}/titles",
            async (Guid requestId, UpdateRequestTitleRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new SyncRequestTitleCommand(Guid.NewGuid(), requestId, request.RequestTitleDtos), cancellationToken);

                var response = new UpdateRequestTitleResponse(result.RequestTitles);

                return Results.Ok(response);
            })
            .WithName("UpdateRequestTitle")
            .Produces<UpdateRequestTitleResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update a request title")
            .WithDescription(
                "Updates an existing title/collateral for the specified request. All title details including land area, building information, vehicle, and machine details can be modified.")
            .WithTags("Request Titles")
            .AllowAnonymous();
        // .RequireAuthorization("CanWriteRequest");
    }
}