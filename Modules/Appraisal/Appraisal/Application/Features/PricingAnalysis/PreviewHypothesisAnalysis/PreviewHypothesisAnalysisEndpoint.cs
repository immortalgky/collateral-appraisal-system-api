using Carter;
using MediatR;
using Appraisal.Application.Features.PricingAnalysis.SaveHypothesisAnalysis;

namespace Appraisal.Application.Features.PricingAnalysis.PreviewHypothesisAnalysis;

public record PreviewHypothesisAnalysisRequest(
    LandBuildingSummaryInput? LandBuildingSummary,
    CondominiumSummaryInput? CondominiumSummary,
    IReadOnlyList<HypothesisCostItemInput> CostItems
);

public class PreviewHypothesisAnalysisEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/pricing-analysis/{pricingAnalysisId:guid}/methods/{methodId:guid}/hypothesis-analysis/preview",
                async (Guid pricingAnalysisId, Guid methodId,
                    PreviewHypothesisAnalysisRequest request, ISender sender) =>
                {
                    var command = new PreviewHypothesisAnalysisCommand(
                        pricingAnalysisId,
                        methodId,
                        request.LandBuildingSummary,
                        request.CondominiumSummary,
                        request.CostItems);

                    var result = await sender.Send(command);
                    return Results.Ok(result);
                })
            .WithName("PreviewHypothesisAnalysis")
            .Produces<PreviewHypothesisAnalysisResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Preview hypothesis analysis")
            .WithDescription("Returns full computed snapshot for hypothesis analysis WITHOUT persisting. Use for live preview while editing.")
            .WithTags("PricingAnalysis");
    }
}
