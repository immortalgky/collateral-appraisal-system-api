using Appraisal.Domain.Appraisals;

namespace Appraisal.Domain.Services;

/// <summary>
/// Direct Comparison calculation service.
/// Same as SaleGrid EXCEPT: no weighting step.
/// FinalValue is appraiser-selected (not computed).
/// </summary>
public class DirectComparisonCalculationService : IPricingCalculationService
{
    public void Recalculate(PricingAnalysisMethod method)
    {
        foreach (var calc in method.Calculations)
        {
            RecalculateForComparable(method, calc);
        }

        // Auto-compute final value = min of all TotalAdjustedValue (most conservative)
        var calcsWithValue = method.Calculations
            .Where(c => c.TotalAdjustedValue.HasValue)
            .ToList();

        if (calcsWithValue.Count > 0)
        {
            var fv = calcsWithValue.Min(c => c.TotalAdjustedValue!.Value);
            var fvRounded = Math.Floor(fv / 10_000m) * 10_000m;

            if (method.FinalValue is null)
            {
                var finalValue = PricingFinalValue.Create(method.Id, fv, fvRounded);
                method.SetFinalValue(finalValue);
            }
            else
            {
                method.FinalValue.UpdateFinalValue(fv, fvRounded);
            }

            method.SetValue(fvRounded);
        }
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

        // Step 3: Factor adjustments
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

        // No weighting step for DirectComparison
    }
}
