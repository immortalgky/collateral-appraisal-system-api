using Carter;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.SaveHypothesisAnalysis;

public class SaveHypothesisAnalysisEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/pricing-analysis/{pricingAnalysisId:guid}/methods/{methodId:guid}/hypothesis-analysis",
                async (Guid pricingAnalysisId, Guid methodId,
                    SaveHypothesisAnalysisRequest request, ISender sender) =>
                {
                    var command = new SaveHypothesisAnalysisCommand(
                        pricingAnalysisId,
                        methodId,
                        request.LandBuildingSummary,
                        request.CondominiumSummary,
                        request.CostItems,
                        request.Remark);

                    var result = await sender.Send(command);
                    return Results.Ok(result);
                })
            .WithName("SaveHypothesisAnalysis")
            .Produces<SaveHypothesisAnalysisResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Save hypothesis analysis")
            .WithDescription("Upserts hypothesis/residual pricing analysis inputs and recalculates all derived values server-side.")
            .WithTags("PricingAnalysis");
    }
}
