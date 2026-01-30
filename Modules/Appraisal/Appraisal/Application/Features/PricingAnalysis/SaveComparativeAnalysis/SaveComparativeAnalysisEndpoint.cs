using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Appraisal.Application.Features.PricingAnalysis.SaveComparativeAnalysis;

public class SaveComparativeAnalysisEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/pricing-analysis/{id:guid}/methods/{methodId:guid}/comparative-analysis",
                async (Guid id, Guid methodId, SaveComparativeAnalysisRequest request, ISender sender) =>
                {
                    var command = new SaveComparativeAnalysisCommand(
                        id,
                        methodId,
                        request.ComparativeFactors,
                        request.FactorScores,
                        request.Calculations
                    );

                    var result = await sender.Send(command);
                    return Results.Ok(result);
                })
            .WithName("SaveComparativeAnalysis")
            .Produces<SaveComparativeAnalysisResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Save entire comparative analysis")
            .WithDescription("Saves Step 1 (factor selection) and Step 2 (factor scoring) in a single transaction")
            .WithTags("PricingAnalysis");
    }
}
