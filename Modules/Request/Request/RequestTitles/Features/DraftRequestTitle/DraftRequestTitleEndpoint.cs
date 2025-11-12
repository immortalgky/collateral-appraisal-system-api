using Request.Extensions;

namespace Request.RequestTitles.Features.DraftRequestTitle;

public class DraftRequestTitleEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/requests/{requestId:Guid}/titles/draft", 
                async (Guid requestId, DraftRequestTitleRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = request.Adapt<DraftRequestTitleCommand>() with { RequestId = requestId };

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<DraftRequestTitleResponse>();

                    return Results.Created($"/requests/{requestId}/titles/{response.Id}", response.Id);
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
