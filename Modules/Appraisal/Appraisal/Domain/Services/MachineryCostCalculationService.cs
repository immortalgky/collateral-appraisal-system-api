using Appraisal.Domain.Appraisals;

namespace Appraisal.Domain.Services;

/// <summary>
/// Recalculates FMV for each MachineCostItem in a MachineryCost method.
/// Formula: FMV = RCN × P × F × E
/// where P = (1 - (N - R) / N) × C, R = N - n
/// n (duration in use) is computed externally from YearOfManufacture.
/// </summary>
public class MachineryCostCalculationService : IPricingCalculationService
{
    public void Recalculate(PricingAnalysisMethod method)
    {
        decimal totalFmv = 0;

        foreach (var item in method.MachineCostItems)
        {
            if (!item.RcnReplacementCost.HasValue ||
                !item.LifeSpanYears.HasValue || item.LifeSpanYears.Value == 0)
            {
                item.SetFairMarketValue(null);
                continue;
            }

            var rcn = item.RcnReplacementCost.Value;
            var n = item.LifeSpanYears.Value; // N (life span)
            var c = item.ConditionFactor;
            var f = item.FunctionalObsolescence;
            var e = item.EconomicObsolescence;

            // P = (1 - (N - R) / N) × C — but since we don't store n (duration in use)
            // and R (residual), the frontend computes FMV client-side.
            // Backend stores the final FMV from the frontend computation.
            // Here we just sum totals.
            if (item.FairMarketValue.HasValue)
                totalFmv += item.FairMarketValue.Value;
        }

        method.SetValue(totalFmv);
    }
}
