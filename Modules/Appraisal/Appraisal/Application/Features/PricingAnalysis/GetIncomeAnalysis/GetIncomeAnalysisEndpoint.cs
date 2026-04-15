using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.GetIncomeAnalysis;

public class GetIncomeAnalysisEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/pricing-analysis/{pricingAnalysisId:guid}/methods/{methodId:guid}/income-analysis",
                async (Guid pricingAnalysisId, Guid methodId, ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var query = new GetIncomeAnalysisQuery(pricingAnalysisId, methodId);
                    var result = await sender.Send(query, cancellationToken);

                    if (result.Analysis is null)
                        return Results.NotFound();

                    var response = result.Adapt<GetIncomeAnalysisResponse>();
                    return Results.Ok(response);
                })
            .WithName("GetIncomeAnalysis")
            .Produces<GetIncomeAnalysisResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get income analysis")
            .WithDescription(
                "Returns the saved income-analysis tree for an Income pricing method. " +
                "Returns 404 when no analysis has been saved yet.")
            .WithTags("PricingAnalysis");
    }
}
