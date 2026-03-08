namespace Appraisal.Application.Features.PricingAnalysis.LinkComparable;

public record LinkComparableResult(
    Guid LinkId,
    Guid CalculationId,
    decimal? OfferPrice,
    decimal? OfferPriceAdjustmentPercent,
    decimal? OfferPriceAdjustmentAmount,
    decimal? SalePrice,
    DateTime? SaleDate);
