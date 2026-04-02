using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.SaveMachineCostItems;

public class SaveMachineCostItemsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/pricing-analysis/{pricingAnalysisId:guid}/methods/{methodId:guid}/machine-cost-items",
                async (Guid pricingAnalysisId, Guid methodId, SaveMachineCostItemsRequest request,
                    ISender sender) =>
                {
                    var command = new SaveMachineCostItemsCommand(
                        pricingAnalysisId,
                        methodId,
                        request.Items,
                        request.Remark
                    );

                    var result = await sender.Send(command);
                    var response = result.Adapt<SaveMachineCostItemsResponse>();
                    return Results.Ok(response);
                })
            .WithName("SaveMachineCostItems")
            .Produces<SaveMachineCostItemsResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Save machine cost items")
            .WithDescription("Bulk save machine cost calculation items for a MachineryCost pricing method. Creates, updates, or removes items.")
            .WithTags("PricingAnalysis");
    }
}
