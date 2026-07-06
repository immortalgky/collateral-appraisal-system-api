using Carter;
using MediatR;

namespace Appraisal.Application.Features.MarketComparableFactors.SetMarketComparableFactorStatus;

public class SetMarketComparableFactorStatusEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/market-comparable-factors/{id:guid}/activate",
            async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                await sender.Send(new SetMarketComparableFactorStatusCommand(id, true), cancellationToken);
                return Results.NoContent();
            })
            .WithName("ActivateMarketComparableFactor")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Activate a market comparable factor")
            .WithTags("MarketComparableFactors");

        app.MapPost("/market-comparable-factors/{id:guid}/deactivate",
            async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                await sender.Send(new SetMarketComparableFactorStatusCommand(id, false), cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeactivateMarketComparableFactor")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Deactivate a market comparable factor")
            .WithTags("MarketComparableFactors");
    }
}
