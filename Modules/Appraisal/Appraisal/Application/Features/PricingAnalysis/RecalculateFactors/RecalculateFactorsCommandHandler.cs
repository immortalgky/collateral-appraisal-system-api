using Appraisal.Domain.Appraisals;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.RecalculateFactors;

/// <summary>
/// Handler for recalculating total factor adjustment.
/// Factor scores are now at the method level.
/// </summary>
public class RecalculateFactorsCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository
) : ICommandHandler<RecalculateFactorsCommand, RecalculateFactorsResult>
{
    public async Task<RecalculateFactorsResult> Handle(
        RecalculateFactorsCommand command,
        CancellationToken cancellationToken)
    {
        // Load pricing analysis aggregate
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdWithAllDataAsync(
            command.PricingAnalysisId,
            cancellationToken);

        if (pricingAnalysis is null)
            throw new InvalidOperationException($"PricingAnalysis {command.PricingAnalysisId} not found");

        // Navigate to the calculation
        var calculation = pricingAnalysis.Approaches
            .SelectMany(a => a.Methods)
            .SelectMany(m => m.Calculations)
            .FirstOrDefault(c => c.Id == command.PricingCalculationId);

        if (calculation is null)
            throw new InvalidOperationException($"PricingCalculation {command.PricingCalculationId} not found");

        // Find the method that owns this calculation
        var method = pricingAnalysis.Approaches
            .SelectMany(a => a.Methods)
            .First(m => m.Calculations.Contains(calculation));

        // Get factor scores for this specific comparable
        var factorScoresForComparable = method.GetFactorScoresForComparable(calculation.MarketComparableId);

        // Calculate total adjustment from factor scores
        var totalAdjustmentPct = factorScoresForComparable
            .Where(f => f.AdjustmentPct.HasValue)
            .Sum(f => f.AdjustmentPct!.Value);

        // Update the calculation's factor adjustment
        calculation.SetFactorAdjustment(totalAdjustmentPct, null);

        // Repository saves via EF change tracking
        await pricingAnalysisRepository.UpdateAsync(pricingAnalysis, cancellationToken);

        return new RecalculateFactorsResult(
            calculation.Id,
            calculation.TotalFactorDiffPct
        );
    }
}
