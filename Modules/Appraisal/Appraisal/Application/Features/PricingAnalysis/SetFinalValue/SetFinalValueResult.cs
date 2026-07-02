namespace Appraisal.Application.Features.PricingAnalysis.SetFinalValue;

/// <summary>
/// Result of setting final value for a pricing method
/// </summary>
public record SetFinalValueResult(
    Guid FinalValueId,
    decimal FinalValue,
    decimal FinalValueRounded,
    bool IncludeLandArea,
    decimal? LandArea,
    decimal? LandValue,
    bool HasBuildingValue,
    decimal? BuildingValue,
    decimal? AppraisalPrice
);
