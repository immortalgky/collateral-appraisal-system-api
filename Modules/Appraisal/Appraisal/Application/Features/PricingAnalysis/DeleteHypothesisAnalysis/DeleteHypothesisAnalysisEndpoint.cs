using Carter;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.DeleteHypothesisAnalysis;

public class DeleteHypothesisAnalysisEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/pricing-analysis/{pricingAnalysisId:guid}/methods/{methodId:guid}/hypothesis-analysis",
                async (Guid pricingAnalysisId, Guid methodId, ISender sender) =>
                {
                    var command = new DeleteHypothesisAnalysisCommand(pricingAnalysisId, methodId);
                    await sender.Send(command);
                    return Results.NoContent();
                })
            .WithName("DeleteHypothesisAnalysis")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Delete / reset hypothesis analysis")
            .WithDescription("Removes the hypothesis analysis (the Reset button). Cascade-deletes all uploads, unit rows, and cost items.")
            .WithTags("PricingAnalysis");
    }
}
