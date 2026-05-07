namespace Appraisal.Application.Features.PricingAnalysis.UpdateFinalValue;

/// <summary>
/// Request to update final value
/// </summary>
public record UpdateFinalValueRequest(
    decimal FinalValue,
    decimal FinalValueRounded,
    bool? IncludeLandArea = null,
    decimal? LandArea = null,
    decimal? LandValue = null,
    bool? HasBuildingCost = null,
    decimal? BuildingCost = null,
    decimal? AppraisalPrice = null
);
