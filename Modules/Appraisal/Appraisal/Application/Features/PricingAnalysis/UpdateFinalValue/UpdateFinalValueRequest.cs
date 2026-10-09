namespace Appraisal.Application.Features.PricingAnalysis.UpdateFinalValue;

/// <summary>
/// Request to update final value
/// </summary>
public record UpdateFinalValueRequest(
    decimal FinalValue,
    bool? IncludeLandArea = null,
    decimal? LandArea = null,
    decimal? LandValue = null,
    bool? HasBuildingValue = null,
    decimal? BuildingValue = null,
    decimal? IndicatedValue = null
);
