using Carter;
using MediatR;

namespace Appraisal.Application.Features.MarketComparables.SetMarketComparableFactorData;

/// <summary>
/// Endpoint: PUT /market-comparables/{id}/factor-data
/// Sets factor data for a market comparable
/// </summary>
public class SetMarketComparableFactorDataEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/market-comparables/{id:guid}/factor-data",
                async (
                    Guid id,
                    SetMarketComparableFactorDataRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new SetMarketComparableFactorDataCommand(
                        id,
                        request.FactorData);

                    var result = await sender.Send(command, cancellationToken);

                    return Results.Ok(new { result.Success });
                }
            )
            .WithName("SetMarketComparableFactorData")
            .Produces<object>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Set factor data for market comparable")
            .WithDescription("Sets or updates multiple factor values for a market comparable.")
            .WithTags("MarketComparables");
    }
}
