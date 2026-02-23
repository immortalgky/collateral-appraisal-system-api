using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.RecalculateFactors;

public class RecalculateFactorsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/pricing-analysis/{id}/calculations/{calcId}/recalculate",
                async (
                    Guid id,
                    Guid calcId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new RecalculateFactorsCommand(id, calcId);

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<RecalculateFactorsResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("RecalculateFactors")
            .Produces<RecalculateFactorsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Recalculate total factor adjustment")
            .WithDescription("Recalculates the total factor difference percentage by summing all factor score adjustments.")
            .WithTags("PricingAnalysis");
    }
}
