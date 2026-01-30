using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.MarketComparables.AddMarketComparableImage;

/// <summary>
/// Endpoint: POST /market-comparables/{id}/images
/// Adds an image to a market comparable by linking a document uploaded via the Document API.
/// </summary>
public class AddMarketComparableImageEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/market-comparables/{id:guid}/images",
                async (
                    Guid id,
                    AddMarketComparableImageRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new AddMarketComparableImageCommand(
                        id,
                        request.DocumentId,
                        request.Title,
                        request.Description);

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<AddMarketComparableImageResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("AddMarketComparableImage")
            .Produces<AddMarketComparableImageResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Add image to market comparable")
            .WithDescription("Links a document (uploaded via Document API) as an image to a market comparable record.")
            .WithTags("MarketComparables");
    }
}
