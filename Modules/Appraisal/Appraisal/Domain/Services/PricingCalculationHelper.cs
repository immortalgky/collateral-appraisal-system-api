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

    /// <summary>
    /// Picks the dominant price unit from a method's calculations.
    /// Per row, prefer the offering unit when offering price is set & non-zero,
    /// else fall back to the selling unit. The most frequent unit wins.
    /// Returns the stored vocabulary value (PerSqWa / PerSqm / PerUnit) or null.
    /// Matches the FE detectPriceUnit helper.
    /// </summary>
    public static string? DetectPriceUnit(IEnumerable<PricingCalculation> calculations)
    {
        var units = calculations
            .Select(c => c.OfferingPrice.HasValue && c.OfferingPrice.Value != 0
                ? c.OfferingPriceUnit
                : c.SellingPriceUnit)
            .Where(u => !string.IsNullOrEmpty(u))
            .Select(u => u!)
            .ToList();

        if (units.Count == 0) return null;

        return units
            .GroupBy(u => u)
            .OrderByDescending(g => g.Count())
            .First().Key;
    }

    /// <summary>
    /// Rounds a computed final value based on the comparable price unit.
    /// PerSqWa / PerSqm (per-Sq.Wa / per-Sq.M rate) → no rounding (prices are small).
    /// Else (PerUnit total price)                   → floor to nearest 1,000.
    /// </summary>
    public static decimal RoundFinalValue(decimal finalValue, IEnumerable<PricingCalculation> calculations)
    {
        var detectedUnit = DetectPriceUnit(calculations);
        var isUnitPrice = PricingUnit.IsPerUnitRate(detectedUnit);
        return isUnitPrice ? finalValue : Math.Floor(finalValue / 1_000m) * 1_000m;
    }

    /// <summary>
    /// Resolves the method's committed price unit from its calculations,
    /// defaulting to a whole-unit lumpsum (PerUnit) when no unit can be detected.
    /// </summary>
    public static string ResolvePriceUnit(IEnumerable<PricingCalculation> calculations)
        => DetectPriceUnit(calculations) ?? PricingUnit.PerUnit;
}
