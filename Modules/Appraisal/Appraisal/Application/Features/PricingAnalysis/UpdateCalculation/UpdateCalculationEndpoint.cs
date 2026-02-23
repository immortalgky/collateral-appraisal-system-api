using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.PricingAnalysis.UpdateCalculation;

public class UpdateCalculationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/pricing-analysis/{pricingAnalysisId:guid}/calculations/{calculationId:guid}",
            async (Guid pricingAnalysisId, Guid calculationId, UpdateCalculationRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                var command = new UpdateCalculationCommand(
                    pricingAnalysisId,
                    calculationId,
                    request.OfferingPrice,
                    request.OfferingPriceUnit,
                    request.AdjustOfferPricePct,
                    request.AdjustOfferPriceAmt,
                    request.SellingPrice,
                    request.SellingPriceUnit,
                    request.BuySellYear,
                    request.BuySellMonth,
                    request.AdjustedPeriodPct,
                    request.CumulativeAdjPeriod,
                    request.TotalInitialPrice,
                    request.LandAreaDeficient,
                    request.LandAreaDeficientUnit,
                    request.LandPrice,
                    request.LandValueAdjustment,
                    request.UsableAreaDeficient,
                    request.UsableAreaDeficientUnit,
                    request.UsableAreaPrice,
                    request.BuildingValueAdjustment,
                    request.TotalFactorDiffPct,
                    request.TotalFactorDiffAmt,
                    request.TotalAdjustedValue,
                    request.Weight);

                var result = await sender.Send(command, cancellationToken);
                var response = result.Adapt<UpdateCalculationResponse>();
                return Results.Ok(response);
            })
            .WithName("UpdateCalculation")
            .Produces<UpdateCalculationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Update pricing calculation")
            .WithDescription("Updates all calculation fields including pricing, time adjustments, area adjustments, and factor adjustments.")
            .WithTags("PricingAnalysis");
    }
}
