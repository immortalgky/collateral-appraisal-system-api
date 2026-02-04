
namespace Appraisal.Application.Features.MarketComparables.UpdateMarketComparable;

public class UpdateMarketComparableEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/market-comparables/{id:guid}", async (Guid id, UpdateMarketComparableCommand request, ISender sender) =>
        {
            var command = request.Adapt<UpdateMarketComparableCommand>() with { Id = id };
            var result = await sender.Send(command);
            return Results.Ok(new UpdateMarketComparableResponse(result.Success));
        }).WithName("UpdateMarketComparable")
            .Produces<UpdateMarketComparableResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("MarketComparables")
            .WithSummary("Update an existing Market Comparable")
            .WithDescription("Updates an existing Market Comparable in the system.")
            .AllowAnonymous();
    }
}
