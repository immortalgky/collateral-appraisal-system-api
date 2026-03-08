using Appraisal.Domain.Appraisals;

namespace Appraisal.Domain.Services;

/// <summary>
/// Shared helper for computing initial price from offer/selling price fields.
/// </summary>
internal static class PricingCalculationHelper
{
    /// <summary>
    /// Computes the initial adjusted price from a PricingCalculation's offer/selling price fields.
    /// Three mutually exclusive paths:
    ///   Path A: OfferPrice × (1 - AdjustPct/100)
    ///   Path B: AdjustOfferPriceAmt (direct override)
    ///   Path C: SellingPrice × (1 + CumulativeAdjPeriod/100)
    /// </summary>
    public static decimal? ComputeInitialPrice(PricingCalculation calc)
    {
        // Path B: Direct amount override takes precedence
        if (calc.AdjustOfferPriceAmt.HasValue && calc.AdjustOfferPriceAmt.Value != 0)
            return calc.AdjustOfferPriceAmt.Value;

        // Path A: Offer price with percentage adjustment
        if (calc.OfferingPrice.HasValue && calc.AdjustOfferPricePct.HasValue)
            return calc.OfferingPrice.Value * (1m - calc.AdjustOfferPricePct.Value / 100m);

        // Path A without adjustment: just the offer price
        if (calc.OfferingPrice.HasValue)
            return calc.OfferingPrice.Value;

        // Path C: Selling price with time adjustment
        if (calc.SellingPrice.HasValue)
        {
            var cumulative = calc.CumulativeAdjPeriod ?? 0m;
            return calc.SellingPrice.Value * (1m + cumulative / 100m);
        }

        return null;
    }

    /// <summary>
    /// Computes BuySellYear and BuySellMonth from a sale date relative to today.
    /// BuySellMonth = total months difference.
    /// BuySellYear = total months / 12, rounded (≥ 0.5 rounds up).
    /// </summary>
    public static (int Years, int Months) ComputeTimeFromSaleDate(DateTime saleDate)
    {
        var today = DateTime.Today;
        var totalMonths = (today.Year - saleDate.Year) * 12 + (today.Month - saleDate.Month);
        if (totalMonths < 0) totalMonths = 0;
        var years = (int)Math.Round(totalMonths / 12.0, MidpointRounding.AwayFromZero);
        return (years, totalMonths);
    }

    /// <summary>
    /// Computes CumulativeAdjPeriod = BuySellYear × AdjustedPeriodPct.
    /// </summary>
    public static decimal ComputeCumulativeAdjPeriod(int years, decimal? adjustedPeriodPct)
    {
        return years * (adjustedPeriodPct ?? 0m);
    }
}
