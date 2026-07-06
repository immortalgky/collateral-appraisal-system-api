using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Appraisal.Application.Features.MarketComparableFactors.DeleteMarketComparableFactor;

/// <summary>
/// Endpoint: DELETE /market-comparable-factors/{id}
/// Soft deletes a market comparable factor
/// </summary>
public class DeleteMarketComparableFactorEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/market-comparable-factors/{id:guid}", async (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new DeleteMarketComparableFactorCommand(id);
                var result = await sender.Send(command, cancellationToken);

                return Results.NoContent();
            })
            .WithName("DeleteMarketComparableFactor")
            .WithSummary("Delete a market comparable factor")
            .WithDescription("Permanently deletes a market comparable factor. Returns 409 if the factor is still used by a template or has existing market comparable data (deactivate it instead).")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithTags("MarketComparableFactors");
    }
}
