namespace Appraisal.Application.Features.PricingAnalysis.UpdateFinalValue;

/// <summary>
/// Response for updating final value
/// </summary>
public record UpdateFinalValueResponse(
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
