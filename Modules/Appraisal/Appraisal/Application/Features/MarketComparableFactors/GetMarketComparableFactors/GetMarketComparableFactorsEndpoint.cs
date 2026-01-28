using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Appraisal.Application.Features.MarketComparableFactors.GetMarketComparableFactors;

/// <summary>
/// Endpoint: GET /market-comparable-factors
/// Retrieves all market comparable factors
/// </summary>
public class GetMarketComparableFactorsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/market-comparable-factors", async (
                ISender sender,
                bool activeOnly = true,
                CancellationToken cancellationToken = default) =>
            {
                var query = new GetMarketComparableFactorsQuery(activeOnly);
                var result = await sender.Send(query, cancellationToken);

                return Results.Ok(result.Factors);
            })
            .WithName("GetMarketComparableFactors")
            .WithSummary("Get all market comparable factors")
            .WithDescription("Retrieves all market comparable factors. Set activeOnly=false to include inactive factors.")
            .Produces<IReadOnlyList<MarketComparableFactorDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("MarketComparableFactors");
    }
}
