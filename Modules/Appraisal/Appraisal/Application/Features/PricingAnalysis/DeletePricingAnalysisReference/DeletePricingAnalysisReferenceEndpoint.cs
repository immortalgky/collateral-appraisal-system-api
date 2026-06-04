using Carter;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.DeletePricingAnalysisReference;

public class DeletePricingAnalysisReferenceEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/pricing-analysis/{id:guid}",
            async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                await sender.Send(new DeletePricingAnalysisReferenceCommand(id), cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeletePricingAnalysisReference")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Delete a market-reference pricing analysis")
            .WithDescription(
                "Deletes a reference PricingAnalysis (and its cascade). Reference subtypes only — " +
                "property-group / project-model valuation analyses are rejected.")
            .WithTags("PricingAnalysis");
    }
}
