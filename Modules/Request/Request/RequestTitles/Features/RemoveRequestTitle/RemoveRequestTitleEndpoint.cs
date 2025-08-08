namespace Request.RequestTitles.Features.RemoveRequestTitle;

public class RemoveRequestTitleEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/requests/{requestId:long}/titles/{titleId:long}",
            async (long requestId, long titleId, ISender sender, CancellationToken cancellationToken) =>
            {
                var command = new RemoveRequestTitleCommand(requestId, titleId);

                var result = await sender.Send(command, cancellationToken);

                var response = result.Adapt<RemoveRequestTitleResponse>();

                return Results.Ok(response);
            })
            .WithName("RemoveRequestTitle")
            .Produces<RemoveRequestTitleResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Remove a request title")
            .WithDescription("Removes an existing title/collateral from the specified request. This action cannot be undone.")
            .WithTags("Request Titles");
    }
}