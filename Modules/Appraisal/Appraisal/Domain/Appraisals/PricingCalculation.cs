namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Calculation details per market comparable comparison.
/// </summary>
public class PricingCalculation : Entity<Guid>
{
    public Guid PricingMethodId { get; private set; }
    public Guid MarketComparableId { get; private set; }

    // Offering/Selling Price
    public decimal? OfferingPrice { get; private set; }
    public string? OfferingPriceUnit { get; private set; } // PerSqWa, PerSqm, PerUnit
    public decimal? AdjustOfferPricePct { get; private set; }
    public decimal? AdjustOfferPriceAmt { get; private set; }
    public decimal? SellingPrice { get; private set; }
    public string? SellingPriceUnit { get; private set; }

    // Time Adjustment
    public int? BuySellYear { get; private set; }
    public int? BuySellMonth { get; private set; }
    public decimal? AdjustedPeriodPct { get; private set; }
    public decimal? CumulativeAdjPeriod { get; private set; }
    public decimal? TotalInitialPrice { get; private set; }

    // Area Adjustment
    public decimal? LandAreaDeficient { get; private set; }
    public string? LandAreaDeficientUnit { get; private set; }
    public decimal? LandPrice { get; private set; }
    public decimal? LandValueAdjustment { get; private set; }
    public decimal? UsableAreaDeficient { get; private set; }
    public string? UsableAreaDeficientUnit { get; private set; }
    public decimal? UsableAreaPrice { get; private set; }
    public decimal? BuildingValueAdjustment { get; private set; }

    // Factor Adjustments
    public decimal? TotalFactorDiffPct { get; private set; }
    public decimal? TotalFactorDiffAmt { get; private set; }

    // Results
    public decimal? TotalAdjustedValue { get; private set; }
    public decimal? Weight { get; private set; }

    private PricingCalculation()
    {
    }

    public static PricingCalculation Create(Guid pricingMethodId, Guid marketComparableId)
    {
        return new PricingCalculation
        {
            Id = Guid.NewGuid(),
            PricingMethodId = pricingMethodId,
            MarketComparableId = marketComparableId
        };
    }

    public void SetOfferingPrice(decimal price, string unit, decimal? adjustPct = null, decimal? adjustAmt = null)
    {
        OfferingPrice = price;
        OfferingPriceUnit = unit;
        AdjustOfferPricePct = adjustPct;
        AdjustOfferPriceAmt = adjustAmt;
    }

    public void SetSellingPrice(decimal price, string? unit = null)
    {
        SellingPrice = price;
        SellingPriceUnit = unit;
    }

    public void SetTimeAdjustment(int? year, int? month, decimal? adjustedPct, decimal? cumulativePct,
        decimal? totalInitial)
    {
        BuySellYear = year;
        BuySellMonth = month;
        AdjustedPeriodPct = adjustedPct;
        CumulativeAdjPeriod = cumulativePct;
        TotalInitialPrice = totalInitial;
    }

    public void SetLandAdjustment(decimal? deficient, string? unit, decimal? price, decimal? adjustment)
    {
        LandAreaDeficient = deficient;
        LandAreaDeficientUnit = unit;
        LandPrice = price;
        LandValueAdjustment = adjustment;
    }

    public void SetBuildingAdjustment(decimal? usableDeficient, string? unit, decimal? price, decimal? adjustment)
    {
        UsableAreaDeficient = usableDeficient;
        UsableAreaDeficientUnit = unit;
        UsableAreaPrice = price;
        BuildingValueAdjustment = adjustment;
    }

    public void SetFactorAdjustment(decimal? diffPct, decimal? diffAmt)
    {
        TotalFactorDiffPct = diffPct;
        TotalFactorDiffAmt = diffAmt;
    }

    public void SetResult(decimal adjustedValue, decimal? weight = null)
    {
        TotalAdjustedValue = adjustedValue;
        Weight = weight;
    }
}