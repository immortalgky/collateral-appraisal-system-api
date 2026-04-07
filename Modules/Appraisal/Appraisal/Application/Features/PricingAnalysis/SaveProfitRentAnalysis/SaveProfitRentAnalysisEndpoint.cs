using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.SaveProfitRentAnalysis;

public class SaveProfitRentAnalysisEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/pricing-analysis/{pricingAnalysisId:guid}/methods/{methodId:guid}/profit-rent-analysis",
                async (Guid pricingAnalysisId, Guid methodId, SaveProfitRentAnalysisRequest request,
                    ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new SaveProfitRentAnalysisCommand(
                        pricingAnalysisId,
                        methodId,
                        request.MarketRentalFeePerSqWa,
                        request.GrowthRateType,
                        request.GrowthRatePercent,
                        request.GrowthIntervalYears,
                        request.DiscountRate,
                        request.IncludeBuildingCost,
                        request.GrowthPeriods,
                        request.Remark,
                        request.EstimatePriceRounded,
                        request.AppraisalPriceWithBuildingRounded
                    );

                    var result = await sender.Send(command, cancellationToken);
                    var response = result.Adapt<SaveProfitRentAnalysisResponse>();
                    return Results.Ok(response);
                })
            .WithName("SaveProfitRentAnalysis")
            .Produces<SaveProfitRentAnalysisResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Save profit rent analysis")
            .WithDescription("Upserts profit rent analysis for a ProfitRent pricing method. Backend recalculates all derived values.")
            .WithTags("PricingAnalysis");
    }
}
