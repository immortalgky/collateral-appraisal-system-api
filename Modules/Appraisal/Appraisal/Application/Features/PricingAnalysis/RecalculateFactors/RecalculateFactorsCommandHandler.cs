using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Services;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.RecalculateFactors;

/// <summary>
/// Handler for recalculating all derived fields on a pricing method.
/// Uses method-specific calculation services (WQS, SaleGrid, DirectComparison).
/// </summary>
public class RecalculateFactorsCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository,
    PricingCalculationServiceResolver calculationServiceResolver
) : ICommandHandler<RecalculateFactorsCommand, RecalculateFactorsResult>
{
    public async Task<RecalculateFactorsResult> Handle(
        RecalculateFactorsCommand command,
        CancellationToken cancellationToken)
    {
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

        // Use method-specific calculation service
        var calculationService = calculationServiceResolver.Resolve(method.MethodType);
        if (calculationService is not null)
        {
            calculationService.Recalculate(method);
        }
        else
        {
            // Fallback: simple SUM for unsupported method types
            var totalAdjustmentPct = method.GetFactorScoresForComparable(calculation.MarketComparableId)
                .Where(f => f.AdjustmentPct.HasValue)
                .Sum(f => f.AdjustmentPct!.Value);
            calculation.SetFactorAdjustment(totalAdjustmentPct, null);
        }

        await pricingAnalysisRepository.UpdateAsync(pricingAnalysis, cancellationToken);

        return new RecalculateFactorsResult(
            calculation.Id,
            calculation.TotalFactorDiffPct
        );
    }
}
