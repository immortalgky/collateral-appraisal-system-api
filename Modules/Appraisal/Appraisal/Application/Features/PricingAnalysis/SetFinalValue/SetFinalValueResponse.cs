namespace Appraisal.Application.Features.PricingAnalysis.SetFinalValue;

/// <summary>
/// Response for setting final value for a pricing method
/// </summary>
public record SetFinalValueResponse(
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
