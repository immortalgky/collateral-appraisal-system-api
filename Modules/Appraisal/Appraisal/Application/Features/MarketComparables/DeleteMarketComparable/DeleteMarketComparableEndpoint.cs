
namespace Appraisal.Application.Features.MarketComparables.DeleteMarketComparable;

public class DeleteMarketComparableEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/market-comparable/{id:guid}",
            async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                var command = new DeleteMarketComparableCommand(id);
                var result = await sender.Send(command, cancellationToken);
                return Results.Ok(result.IsSuccess);
            }) .WithName("DeleteMarketComparable")
            .Produces<DeleteMarketComparableResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("MarketComparables")
            .WithSummary("Delete market comparable by ID")
            .WithDescription("Deletes a market comparable by its ID.")
            .AllowAnonymous();
    }
}
