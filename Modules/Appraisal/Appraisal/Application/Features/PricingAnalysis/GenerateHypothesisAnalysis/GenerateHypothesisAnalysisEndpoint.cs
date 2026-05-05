using Carter;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.GenerateHypothesisAnalysis;

public class GenerateHypothesisAnalysisEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/pricing-analysis/{pricingAnalysisId:guid}/methods/{methodId:guid}/hypothesis-analysis",
                async (Guid pricingAnalysisId, Guid methodId,
                    GenerateHypothesisAnalysisRequest request, ISender sender) =>
                {
                    var command = new GenerateHypothesisAnalysisCommand(
                        pricingAnalysisId, methodId, request.Variant);

                    var result = await sender.Send(command);
                    return Results.Ok(result);
                })
            .WithName("GenerateHypothesisAnalysis")
            .Produces<GenerateHypothesisAnalysisResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Generate hypothesis analysis")
            .WithDescription("Creates a new hypothesis/residual pricing analysis for the given method and seeds default cost rows.")
            .WithTags("PricingAnalysis");
    }
}
