namespace Appraisal.Application.Features.PricingAnalysis.UpdateFinalValue;

/// <summary>
/// Request to update final value
/// </summary>
public record UpdateFinalValueRequest(
    decimal FinalValue,
    decimal FinalValueRounded,
    bool? IncludeLandArea = null,
    decimal? LandArea = null,
    decimal? AppraisalPrice = null,
    decimal? AppraisalPriceRounded = null,
    bool? HasBuildingCost = null,
    decimal? BuildingCost = null,
    decimal? AppraisalPriceWithBuilding = null,
    decimal? AppraisalPriceWithBuildingRounded = null
);
