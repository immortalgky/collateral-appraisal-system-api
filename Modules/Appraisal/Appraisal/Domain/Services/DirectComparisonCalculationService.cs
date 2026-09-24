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
            var fvRounded = PricingCalculationHelper.RoundFinalValue(fv, method.Calculations);

            if (method.FinalValue is null)
            {
                var finalValue = PricingFinalValue.Create(method.Id, fvRounded);
                method.SetFinalValue(finalValue);
            }
            else
            {
                method.FinalValue.UpdateFinalValue(fvRounded);
            }

            // Persist the resolved price unit (PerSqWa/PerSqm → per-unit rate; PerUnit → lumpsum).
            var unitType = PricingCalculationHelper.ResolvePriceUnit(method.Calculations);
            var valuePerUnit = PricingUnit.IsPerUnitRate(unitType) ? fvRounded : (decimal?)null;
            method.SetValue(fvRounded, valuePerUnit, unitType);
        }
    }

    private static void RecalculateForComparable(PricingAnalysisMethod method, PricingCalculation calc)
    {
        // Step 1: Initial price
        var initialPrice = PricingCalculationHelper.ComputeInitialPrice(calc) ?? 0m;

        // Step 2: 2nd Revision — recalculate area adjustments
        // Rounded to satang at every step, matching the screen — see PricingCalculationHelper.Round2
        // and the identical sequence in SaleGridCalculationService.
        var landValueAdj = PricingCalculationHelper.Round2(
            (calc.LandAreaDeficient ?? 0m) * (calc.LandPrice ?? 0m));
        calc.SetLandAdjustment(calc.LandAreaDeficient, calc.LandAreaDeficientUnit, calc.LandPrice, landValueAdj);

        var buildingValueAdj = PricingCalculationHelper.Round2(
            (calc.UsableAreaDeficient ?? 0m) * (calc.UsableAreaPrice ?? 0m));
        calc.SetBuildingAdjustment(calc.UsableAreaDeficient, calc.UsableAreaDeficientUnit, calc.UsableAreaPrice, buildingValueAdj);

        var totalSecondRevision = PricingCalculationHelper.Round2(
            initialPrice + landValueAdj + buildingValueAdj);

        // Step 3: Factor adjustments
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

        // No weighting step for DirectComparison
    }
}
