namespace Appraisal.Application.Features.PricingAnalysis.UpdateFinalValue;

/// <summary>
/// Result of updating final value
/// </summary>
public record UpdateFinalValueResult(
    Guid FinalValueId,
    decimal FinalValue,
    decimal FinalValueRounded,
    bool IncludeLandArea,
    decimal? LandArea,
    decimal? AppraisalPrice,
    decimal? AppraisalPriceRounded,
    bool HasBuildingCost,
    decimal? BuildingCost,
    decimal? AppraisalPriceWithBuilding,
    decimal? AppraisalPriceWithBuildingRounded
);
