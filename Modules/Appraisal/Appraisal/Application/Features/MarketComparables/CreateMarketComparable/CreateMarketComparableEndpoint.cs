using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.MarketComparables.CreateMarketComparable;

public class CreateMarketComparableEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/market-comparables",
                async (
                    CreateMarketComparableRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<CreateMarketComparableCommand>();

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<CreateMarketComparableResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("CreateMarketComparable")
            .Produces<CreateMarketComparableResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Create new market comparable")
            .WithDescription("Create a new market comparable record for property valuation reference.")
            .WithTags("MarketComparable");
    }
}