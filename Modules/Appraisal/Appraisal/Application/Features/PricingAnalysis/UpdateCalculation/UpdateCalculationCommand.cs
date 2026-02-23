using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.UpdateCalculation;

public record UpdateCalculationCommand(
    Guid PricingAnalysisId,
    Guid CalculationId,
    // Offering/Selling Price
    decimal? OfferingPrice,
    string? OfferingPriceUnit,
    decimal? AdjustOfferPricePct,
    decimal? AdjustOfferPriceAmt,
    decimal? SellingPrice,
    string? SellingPriceUnit,
    // Time Adjustment
    int? BuySellYear,
    int? BuySellMonth,
    decimal? AdjustedPeriodPct,
    decimal? CumulativeAdjPeriod,
    decimal? TotalInitialPrice,
    // Land Adjustment
    decimal? LandAreaDeficient,
    string? LandAreaDeficientUnit,
    decimal? LandPrice,
    decimal? LandValueAdjustment,
    // Building Adjustment
    decimal? UsableAreaDeficient,
    string? UsableAreaDeficientUnit,
    decimal? UsableAreaPrice,
    decimal? BuildingValueAdjustment,
    // Factor Adjustment
    decimal? TotalFactorDiffPct,
    decimal? TotalFactorDiffAmt,
    // Results
    decimal? TotalAdjustedValue,
    decimal? Weight
) : ICommand<UpdateCalculationResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
