using Appraisal.Domain.Appraisals;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.UpdateCalculation;

public class UpdateCalculationCommandHandler(
    IPricingAnalysisRepository repository
) : ICommandHandler<UpdateCalculationCommand, UpdateCalculationResult>
{
    public async Task<UpdateCalculationResult> Handle(
        UpdateCalculationCommand command,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await repository.GetByIdWithAllDataAsync(command.PricingAnalysisId, cancellationToken);

        if (pricingAnalysis is null)
        {
            throw new InvalidOperationException($"Pricing analysis with ID {command.PricingAnalysisId} not found.");
        }

        // Find the calculation across all methods
        var calculation = pricingAnalysis.Approaches
            .SelectMany(a => a.Methods)
            .SelectMany(m => m.Calculations)
            .FirstOrDefault(c => c.Id == command.CalculationId);

        if (calculation is null)
        {
            throw new InvalidOperationException($"Calculation with ID {command.CalculationId} not found.");
        }

        // Update offering price if provided
        if (command.OfferingPrice.HasValue)
        {
            calculation.SetOfferingPrice(
                command.OfferingPrice.Value,
                command.OfferingPriceUnit ?? "PerSqm",
                command.AdjustOfferPricePct,
                command.AdjustOfferPriceAmt);
        }

        // Update selling price if provided
        if (command.SellingPrice.HasValue)
        {
            calculation.SetSellingPrice(command.SellingPrice.Value, command.SellingPriceUnit);
        }

        // Update time adjustment if any values provided
        if (command.BuySellYear.HasValue || command.BuySellMonth.HasValue ||
            command.AdjustedPeriodPct.HasValue || command.CumulativeAdjPeriod.HasValue ||
            command.TotalInitialPrice.HasValue)
        {
            calculation.SetTimeAdjustment(
                command.BuySellYear,
                command.BuySellMonth,
                command.AdjustedPeriodPct,
                command.CumulativeAdjPeriod);
        }

        // Update land adjustment if provided
        if (command.LandAreaDeficient.HasValue || command.LandPrice.HasValue || command.LandValueAdjustment.HasValue)
        {
            calculation.SetLandAdjustment(
                command.LandAreaDeficient,
                command.LandAreaDeficientUnit,
                command.LandPrice,
                command.LandValueAdjustment);
        }

        // Update building adjustment if provided
        if (command.UsableAreaDeficient.HasValue || command.UsableAreaPrice.HasValue || command.BuildingValueAdjustment.HasValue)
        {
            calculation.SetBuildingAdjustment(
                command.UsableAreaDeficient,
                command.UsableAreaDeficientUnit,
                command.UsableAreaPrice,
                command.BuildingValueAdjustment);
        }

        // Update factor adjustment if provided
        if (command.TotalFactorDiffPct.HasValue || command.TotalFactorDiffAmt.HasValue)
        {
            calculation.SetFactorAdjustment(
                command.TotalFactorDiffPct,
                command.TotalFactorDiffAmt);
        }

        // Update result if provided
        if (command.TotalAdjustedValue.HasValue)
        {
            calculation.SetResult(command.TotalAdjustedValue.Value);
        }

        await repository.UpdateAsync(pricingAnalysis, cancellationToken);

        return new UpdateCalculationResult(calculation.Id);
    }
}
