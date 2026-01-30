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
    decimal? AppraisalPrice,
    decimal? AppraisalPriceRounded,
    bool HasBuildingCost,
    decimal? BuildingCost,
    decimal? AppraisalPriceWithBuilding,
    decimal? AppraisalPriceWithBuildingRounded
);
