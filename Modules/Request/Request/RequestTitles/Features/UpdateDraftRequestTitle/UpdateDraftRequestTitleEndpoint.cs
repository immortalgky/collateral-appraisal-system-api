namespace Request.RequestTitles.Features.UpdateDraftRequestTitle;

public class UpdateDraftRequestTitleEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch("/requests/{requestId:guid}/titles/{titleId:Guid}/draft", 
            async (Guid requestId, Guid titleId, UpdateDraftRequestTitleRequest request, ISender sender, CancellationToken cancellationToken) =>
            {

                var command = request.Adapt<UpdateDraftRequestTitleCommand>() with
                {
                    Id = titleId,
                    RequestId = requestId
                };

                var result = await sender.Send(command, cancellationToken);

                var response = result.Adapt<UpdateDraftRequestTitleResponse>();

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
