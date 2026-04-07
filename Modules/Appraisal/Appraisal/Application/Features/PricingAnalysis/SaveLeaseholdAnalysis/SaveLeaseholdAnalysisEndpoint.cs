using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.SaveLeaseholdAnalysis;

public class SaveLeaseholdAnalysisEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/pricing-analysis/{pricingAnalysisId:guid}/methods/{methodId:guid}/leasehold-analysis",
                async (Guid pricingAnalysisId, Guid methodId, SaveLeaseholdAnalysisRequest request,
                    ISender sender) =>
                {
                    var command = new SaveLeaseholdAnalysisCommand(
                        pricingAnalysisId,
                        methodId,
                        request.LandValuePerSqWa,
                        request.LandGrowthRateType,
                        request.LandGrowthRatePercent,
                        request.LandGrowthIntervalYears,
                        request.ConstructionCostIndex,
                        request.InitialBuildingValue,
                        request.DepreciationRate,
                        request.DepreciationIntervalYears,
                        request.BuildingCalcStartYear,
                        request.DiscountRate,
                        request.LandGrowthPeriods,
                        request.IsPartialUsage,
                        request.PartialRai,
                        request.PartialNgan,
                        request.PartialWa,
                        request.PricePerSqWa,
                        request.Remark,
                        request.EstimatePriceRounded
                    );

                    var result = await sender.Send(command);
                    var response = result.Adapt<SaveLeaseholdAnalysisResponse>();
                    return Results.Ok(response);
                })
            .WithName("SaveLeaseholdAnalysis")
            .Produces<SaveLeaseholdAnalysisResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Save leasehold analysis")
            .WithDescription("Upserts leasehold analysis for a Leasehold pricing method. Backend recalculates all derived values.")
            .WithTags("PricingAnalysis");
    }
}
