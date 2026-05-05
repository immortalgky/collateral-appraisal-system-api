using Carter;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.GetHypothesisAnalysis;

public class GetHypothesisAnalysisEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/pricing-analysis/{pricingAnalysisId:guid}/methods/{methodId:guid}/hypothesis-analysis",
                async (Guid pricingAnalysisId, Guid methodId, ISender sender) =>
                {
                    var query = new GetHypothesisAnalysisQuery(pricingAnalysisId, methodId);
                    var result = await sender.Send(query);
                    return Results.Ok(result);
                })
            .WithName("GetHypothesisAnalysis")
            .Produces<GetHypothesisAnalysisResult>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get hypothesis analysis")
            .WithDescription("Returns the full hypothesis analysis for a method, including uploads, unit rows, cost items, and computed summary.")
            .WithTags("PricingAnalysis");
    }
}
