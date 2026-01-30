using Carter;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.DeleteCalculation;

public class DeleteCalculationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/pricing-analysis/{pricingAnalysisId:guid}/calculations/{calculationId:guid}",
            async (Guid pricingAnalysisId, Guid calculationId, ISender sender, CancellationToken cancellationToken) =>
            {
                var command = new DeleteCalculationCommand(pricingAnalysisId, calculationId);
                var result = await sender.Send(command, cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeleteCalculation")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Delete pricing calculation")
            .WithDescription("Removes a pricing calculation from a pricing method.")
            .WithTags("PricingAnalysis");
    }
}
