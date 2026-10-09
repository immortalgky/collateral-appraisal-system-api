using Appraisal.Domain.Appraisals;

namespace Appraisal.Domain.Services;

/// <summary>
/// Sale Adjustment Grid calculation service.
/// Recalculates: InitialPrice, LandValueAdj, BuildingValueAdj, FactorAdjustmentAmts,
/// TotalFactorDiff, TotalAdjustedValue, WeightedAdjustedValue.
/// </summary>
public class SaleGridCalculationService : IPricingCalculationService
{
    public void Recalculate(PricingAnalysisMethod method)
    {
        foreach (var calc in method.Calculations)
        {
            RecalculateForComparable(method, calc);
        }

        // Step 6: Auto-compute final value = sum of weighted adjusted values
        var finalValue = method.Calculations
            .Where(c => c.WeightedAdjustedValue.HasValue)
            .Sum(c => c.WeightedAdjustedValue!.Value);

        var finalValueRounded = PricingCalculationHelper.RoundFinalValue(finalValue, method.Calculations);

        if (method.FinalValue is null)
        {
            var fv = PricingFinalValue.Create(method.Id, finalValueRounded);
            method.SetFinalValue(fv);
        }
        else
        {
            method.FinalValue.UpdateFinalValue(finalValueRounded);
        }

        // Persist the resolved price unit (PerSqWa/PerSqm → per-unit rate; PerUnit → lumpsum).
        var unitType = PricingCalculationHelper.ResolvePriceUnit(method.Calculations);
        var valuePerUnit = PricingUnit.IsPerUnitRate(unitType) ? finalValueRounded : (decimal?)null;
        method.SetValue(finalValueRounded, valuePerUnit, unitType);
    }

    private static void RecalculateForComparable(PricingAnalysisMethod method, PricingCalculation calc)
    {
        // Step 1: Initial price
        var initialPrice = PricingCalculationHelper.ComputeInitialPrice(calc) ?? 0m;

        // Step 2: 2nd Revision — recalculate area adjustments
        // Every intermediate is rounded to satang at the same step the screen rounds it — see
        // PricingCalculationHelper.Round2. Rounding at each step, not once at the end, because the
        // screen does: matching only the final figure would still restore different cells.
        var landValueAdj = PricingCalculationHelper.Round2(
            (calc.LandAreaDeficient ?? 0m) * (calc.LandPrice ?? 0m));
        calc.SetLandAdjustment(calc.LandAreaDeficient, calc.LandAreaDeficientUnit, calc.LandPrice, landValueAdj);

        var buildingValueAdj = PricingCalculationHelper.Round2(
            (calc.UsableAreaDeficient ?? 0m) * (calc.UsableAreaPrice ?? 0m));
        calc.SetBuildingAdjustment(calc.UsableAreaDeficient, calc.UsableAreaDeficientUnit, calc.UsableAreaPrice, buildingValueAdj);

        var totalSecondRevision = PricingCalculationHelper.Round2(
            initialPrice + landValueAdj + buildingValueAdj);

        // Step 3: Factor adjustments — recalculate AdjustmentAmt per factor
        var factorScores = method.GetFactorScoresForComparable(calc.MarketComparableId).ToList();

        decimal totalFactorDiffPct = 0m;
        decimal totalFactorDiffAmt = 0m;

        foreach (var score in factorScores)
        {
            if (score.AdjustmentPct.HasValue)
            {
                var amt = PricingCalculationHelper.Round2(
                    totalSecondRevision * (score.AdjustmentPct.Value / 100m));
                score.SetAdjustment(score.AdjustmentPct, amt, score.ComparisonResult, score.Remarks);
                totalFactorDiffPct += score.AdjustmentPct.Value;
                totalFactorDiffAmt += amt;
            }
        }

        calc.SetFactorAdjustment(totalFactorDiffPct, PricingCalculationHelper.Round2(totalFactorDiffAmt));

        // Step 4: Total adjusted value
        var totalAdjustedValue = PricingCalculationHelper.Round2(
            totalSecondRevision + PricingCalculationHelper.Round2(totalFactorDiffAmt));
        calc.SetResult(totalAdjustedValue);

        // Step 5: Weighted adjusted value (SaleGrid uses weighting)
        if (calc.Weight.HasValue)
        {
            var weightedValue = PricingCalculationHelper.Round2(totalAdjustedValue * calc.Weight.Value);
            calc.SetWeight(calc.Weight, weightedValue);
        }
    }
}
