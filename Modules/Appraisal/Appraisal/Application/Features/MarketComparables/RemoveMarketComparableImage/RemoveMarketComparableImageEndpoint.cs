using Carter;
using MediatR;

namespace Appraisal.Application.Features.MarketComparables.RemoveMarketComparableImage;

/// <summary>
/// Endpoint: DELETE /market-comparables/{id}/images/{imageId}
/// Removes an image from a market comparable
/// </summary>
public class RemoveMarketComparableImageEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/market-comparables/{id:guid}/images/{imageId:guid}",
                async (
                    Guid id,
                    Guid imageId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new RemoveMarketComparableImageCommand(id, imageId);

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("RemoveMarketComparableImage")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Remove image from market comparable")
            .WithDescription("Removes an image from a market comparable record.")
            .WithTags("MarketComparables");
    }
}
