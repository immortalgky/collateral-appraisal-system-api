namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Calculation details per market comparable comparison.
/// FactorScores have been moved to PricingAnalysisMethod level.
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

    // Weighting (SaleGrid only)
    public decimal? Weight { get; private set; }
    public decimal? WeightedAdjustedValue { get; private set; }

    private PricingCalculation()
    {
    }

    public static PricingCalculation Create(Guid pricingMethodId, Guid marketComparableId)
    {
        return new PricingCalculation
        {
            Id = Guid.CreateVersion7(),
            PricingMethodId = pricingMethodId,
            MarketComparableId = marketComparableId
        };
    }

    /// <summary>Deep-clone for CI carry-forward — copies all calculation fields verbatim under a new method.</summary>
    public static PricingCalculation CloneForMethod(PricingCalculation source, Guid newMethodId)
    {
        return new PricingCalculation
        {
            Id = Guid.CreateVersion7(),
            PricingMethodId = newMethodId,
            MarketComparableId = source.MarketComparableId,
            OfferingPrice = source.OfferingPrice,
            OfferingPriceUnit = source.OfferingPriceUnit,
            AdjustOfferPricePct = source.AdjustOfferPricePct,
            AdjustOfferPriceAmt = source.AdjustOfferPriceAmt,
            SellingPrice = source.SellingPrice,
            SellingPriceUnit = source.SellingPriceUnit,
            BuySellYear = source.BuySellYear,
            BuySellMonth = source.BuySellMonth,
            AdjustedPeriodPct = source.AdjustedPeriodPct,
            CumulativeAdjPeriod = source.CumulativeAdjPeriod,
            LandAreaDeficient = source.LandAreaDeficient,
            LandAreaDeficientUnit = source.LandAreaDeficientUnit,
            LandPrice = source.LandPrice,
            LandValueAdjustment = source.LandValueAdjustment,
            UsableAreaDeficient = source.UsableAreaDeficient,
            UsableAreaDeficientUnit = source.UsableAreaDeficientUnit,
            UsableAreaPrice = source.UsableAreaPrice,
            BuildingValueAdjustment = source.BuildingValueAdjustment,
            TotalFactorDiffPct = source.TotalFactorDiffPct,
            TotalFactorDiffAmt = source.TotalFactorDiffAmt,
            TotalAdjustedValue = source.TotalAdjustedValue,
            Weight = source.Weight,
            WeightedAdjustedValue = source.WeightedAdjustedValue
        };
    }

    public void SetOfferingPrice(decimal price, string? unit, decimal? adjustPct = null, decimal? adjustAmt = null)
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

    public void ClearOfferingPrice()
    {
        OfferingPrice = null;
        OfferingPriceUnit = null;
        AdjustOfferPricePct = null;
        AdjustOfferPriceAmt = null;
    }

    public void ClearSellingPrice()
    {
        SellingPrice = null;
        SellingPriceUnit = null;
    }

    public void SetTimeAdjustment(int? year, int? month, decimal? adjustedPct, decimal? cumulativePct)
    {
        BuySellYear = year;
        BuySellMonth = month;
        AdjustedPeriodPct = adjustedPct;
        CumulativeAdjPeriod = cumulativePct;
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

    public void SetResult(decimal adjustedValue)
    {
        TotalAdjustedValue = adjustedValue;
    }

    public void SetWeight(decimal? weight, decimal? weightedAdjustedValue)
    {
        Weight = weight;
        WeightedAdjustedValue = weightedAdjustedValue;
    }
}