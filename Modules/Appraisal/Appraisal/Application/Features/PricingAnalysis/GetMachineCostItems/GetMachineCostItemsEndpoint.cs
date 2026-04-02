using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.GetMachineCostItems;

public class GetMachineCostItemsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/pricing-analysis/{pricingAnalysisId:guid}/methods/{methodId:guid}/machine-cost-items",
                async (Guid pricingAnalysisId, Guid methodId, ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var query = new GetMachineCostItemsQuery(pricingAnalysisId, methodId);
                    var result = await sender.Send(query, cancellationToken);
                    var response = result.Adapt<GetMachineCostItemsResponse>();
                    return Results.Ok(response);
                })
            .WithName("GetMachineCostItems")
            .Produces<GetMachineCostItemsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get machine cost items")
            .WithDescription("Get saved machine cost calculation items for a MachineryCost pricing method.")
            .WithTags("PricingAnalysis");
    }
}
