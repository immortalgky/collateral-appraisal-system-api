namespace Appraisal.Application.Features.PricingAnalysis.UpdateCalculation;

public record UpdateCalculationRequest(
    // Offering/Selling Price
    decimal? OfferingPrice = null,
    string? OfferingPriceUnit = null,
    decimal? AdjustOfferPricePct = null,
    decimal? AdjustOfferPriceAmt = null,
    decimal? SellingPrice = null,
    string? SellingPriceUnit = null,
    // Time Adjustment
    int? BuySellYear = null,
    int? BuySellMonth = null,
    decimal? AdjustedPeriodPct = null,
    decimal? CumulativeAdjPeriod = null,
    decimal? TotalInitialPrice = null,
    // Land Adjustment
    decimal? LandAreaDeficient = null,
    string? LandAreaDeficientUnit = null,
    decimal? LandPrice = null,
    decimal? LandValueAdjustment = null,
    // Building Adjustment
    decimal? UsableAreaDeficient = null,
    string? UsableAreaDeficientUnit = null,
    decimal? UsableAreaPrice = null,
    decimal? BuildingValueAdjustment = null,
    // Factor Adjustment
    decimal? TotalFactorDiffPct = null,
    decimal? TotalFactorDiffAmt = null,
    // Results
    decimal? TotalAdjustedValue = null,
    decimal? Weight = null
);
