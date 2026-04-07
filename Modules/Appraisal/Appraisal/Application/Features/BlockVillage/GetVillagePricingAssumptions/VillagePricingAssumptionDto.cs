namespace Appraisal.Application.Features.BlockVillage.GetVillagePricingAssumptions;

public record VillageModelAssumptionDto(
    Guid VillageModelId,
    string? ModelType,
    string? ModelDescription,
    decimal? UsableAreaFrom,
    decimal? UsableAreaTo,
    decimal? StandardLandPrice,
    decimal? StandardPrice,
    decimal? CoverageAmount,
    string? FireInsuranceCondition
);

public record VillagePricingAssumptionDto(
    Guid Id,
    Guid AppraisalId,
    string? LocationMethod,
    decimal? CornerAdjustment,
    decimal? EdgeAdjustment,
    decimal? NearGardenAdjustment,
    decimal? OtherAdjustment,
    decimal? LandIncreaseDecreaseRate,
    decimal? ForceSalePercentage,
    List<VillageModelAssumptionDto> ModelAssumptions
);
