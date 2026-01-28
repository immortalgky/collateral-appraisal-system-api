using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.MarketComparables.GetMarketComparableById;

/// <summary>
/// Endpoint: GET /market-comparables/{id}
/// Gets a market comparable by ID with full details including factor data and images
/// </summary>
public class GetMarketComparableByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/market-comparables/{id:guid}",
                async (
                    Guid id,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetMarketComparableByIdQuery(id);

                    var result = await sender.Send(query, cancellationToken);

                    var response = result.Adapt<GetMarketComparableByIdResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetMarketComparableById")
            .Produces<GetMarketComparableByIdResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get market comparable by ID")
            .WithDescription("Retrieves a market comparable with full details including factor data and images.")
            .WithTags("MarketComparables");
    }
}
