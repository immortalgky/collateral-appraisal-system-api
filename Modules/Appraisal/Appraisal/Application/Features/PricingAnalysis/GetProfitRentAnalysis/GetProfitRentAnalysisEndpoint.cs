using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.GetProfitRentAnalysis;

public class GetProfitRentAnalysisEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/pricing-analysis/{pricingAnalysisId:guid}/methods/{methodId:guid}/profit-rent-analysis",
                async (Guid pricingAnalysisId, Guid methodId, ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var query = new GetProfitRentAnalysisQuery(pricingAnalysisId, methodId);
                    var result = await sender.Send(query, cancellationToken);
                    var response = result.Adapt<GetProfitRentAnalysisResponse>();
                    return Results.Ok(response);
                })
            .WithName("GetProfitRentAnalysis")
            .Produces<GetProfitRentAnalysisResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get profit rent analysis")
            .WithDescription("Get saved profit rent analysis data for a ProfitRent pricing method.")
            .WithTags("PricingAnalysis");
    }
}
