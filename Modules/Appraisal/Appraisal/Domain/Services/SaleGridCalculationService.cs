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

        var finalValueRounded = Math.Floor(finalValue / 10_000m) * 10_000m;

        if (method.FinalValue is null)
        {
            var fv = PricingFinalValue.Create(method.Id, finalValue, finalValueRounded);
            method.SetFinalValue(fv);
        }
        else
        {
            method.FinalValue.UpdateFinalValue(finalValue, finalValueRounded);
        }

        method.SetValue(finalValueRounded);
    }

    private static void RecalculateForComparable(PricingAnalysisMethod method, PricingCalculation calc)
    {
        // Step 1: Initial price
        var initialPrice = PricingCalculationHelper.ComputeInitialPrice(calc) ?? 0m;

        // Step 2: 2nd Revision — recalculate area adjustments
        var landValueAdj = (calc.LandAreaDeficient ?? 0m) * (calc.LandPrice ?? 0m);
        calc.SetLandAdjustment(calc.LandAreaDeficient, calc.LandAreaDeficientUnit, calc.LandPrice, landValueAdj);

        var buildingValueAdj = (calc.UsableAreaDeficient ?? 0m) * (calc.UsableAreaPrice ?? 0m);
        calc.SetBuildingAdjustment(calc.UsableAreaDeficient, calc.UsableAreaDeficientUnit, calc.UsableAreaPrice, buildingValueAdj);

        var totalSecondRevision = initialPrice + landValueAdj + buildingValueAdj;

        // Step 3: Factor adjustments — recalculate AdjustmentAmt per factor
        var factorScores = method.GetFactorScoresForComparable(calc.MarketComparableId).ToList();

        decimal totalFactorDiffPct = 0m;
        decimal totalFactorDiffAmt = 0m;

        foreach (var score in factorScores)
        {
            if (score.AdjustmentPct.HasValue)
            {
                var amt = totalSecondRevision * (score.AdjustmentPct.Value / 100m);
                score.SetAdjustment(score.AdjustmentPct, amt, score.ComparisonResult, score.Remarks);
                totalFactorDiffPct += score.AdjustmentPct.Value;
                totalFactorDiffAmt += amt;
            }
        }

        calc.SetFactorAdjustment(totalFactorDiffPct, totalFactorDiffAmt);

        // Step 4: Total adjusted value
        var totalAdjustedValue = totalSecondRevision + totalFactorDiffAmt;
        calc.SetResult(totalAdjustedValue);

        // Step 5: Weighted adjusted value (SaleGrid uses weighting)
        if (calc.Weight.HasValue)
        {
            var weightedValue = totalAdjustedValue * calc.Weight.Value;
            calc.SetWeight(calc.Weight, weightedValue);
        }
    }
}
