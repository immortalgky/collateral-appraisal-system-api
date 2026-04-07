using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.GetLeaseholdAnalysis;

public class GetLeaseholdAnalysisEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/pricing-analysis/{pricingAnalysisId:guid}/methods/{methodId:guid}/leasehold-analysis",
                async (Guid pricingAnalysisId, Guid methodId, ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var query = new GetLeaseholdAnalysisQuery(pricingAnalysisId, methodId);
                    var result = await sender.Send(query, cancellationToken);
                    var response = result.Adapt<GetLeaseholdAnalysisResponse>();
                    return Results.Ok(response);
                })
            .WithName("GetLeaseholdAnalysis")
            .Produces<GetLeaseholdAnalysisResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get leasehold analysis")
            .WithDescription("Get saved leasehold analysis data for a Leasehold pricing method.")
            .WithTags("PricingAnalysis");
    }
}
