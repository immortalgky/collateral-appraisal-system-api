namespace Appraisal.Application.Features.PricingAnalysis.SetFinalValue;

/// <summary>
/// Request to set final value for a pricing method
/// </summary>
public record SetFinalValueRequest(
    decimal FinalValue,
    decimal FinalValueRounded,
    bool? IncludeLandArea = null,
    decimal? LandArea = null,
    decimal? LandValue = null,
    bool? HasBuildingValue = null,
    decimal? BuildingValue = null,
    decimal? AppraisalPrice = null
);
