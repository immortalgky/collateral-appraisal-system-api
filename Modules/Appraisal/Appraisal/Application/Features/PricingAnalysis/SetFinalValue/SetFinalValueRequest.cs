namespace Appraisal.Application.Features.PricingAnalysis.SetFinalValue;

/// <summary>
/// Request to set final value for a pricing method
/// </summary>
public record SetFinalValueRequest(
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
