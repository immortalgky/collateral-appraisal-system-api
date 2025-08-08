namespace Request.RequestTitles.Features.AddRequestTitle;

public class AddRequestTitleEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/requests/{requestId:long}/titles",
            async (long requestId, AddRequestTitleRequest request, ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = request.Adapt<AddRequestTitleCommand>() with { RequestId = requestId };

                var result = await sender.Send(command, cancellationToken);

                var response = result.Adapt<AddRequestTitleResponse>();

                return Results.Created($"/requests/{requestId}/titles/{response.Id}", response);
            })
            .WithName("AddRequestTitle")
            .Produces<AddRequestTitleResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Add a new request title")
            .WithDescription("Adds a new title/collateral for the specified request. The title details including land area, building information, vehicle, and machine details are provided in the request body.")
            .WithTags("Request Titles");
    }
}