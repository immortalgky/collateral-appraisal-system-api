namespace Request.RequestTitles.Features.UpdateRequestTitle;

public class UpdateRequestTitleEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch("/requests/{requestId:long}/titles/{titleId:long}",
            async (long requestId, long titleId, UpdateRequestTitleRequest request, ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = request.Adapt<UpdateRequestTitleCommand>() with 
                { 
                    RequestId = requestId,
                    Id = titleId
                };

                var result = await sender.Send(command, cancellationToken);

                var response = result.Adapt<UpdateRequestTitleResponse>();

                return Results.Ok(response);
            })
            .WithName("UpdateRequestTitle")
            .Produces<UpdateRequestTitleResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update a request title")
            .WithDescription("Updates an existing title/collateral for the specified request. All title details including land area, building information, vehicle, and machine details can be modified.")
            .WithTags("Request Titles");
    }
}