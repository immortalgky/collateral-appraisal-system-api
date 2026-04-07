namespace Appraisal.Application.Features.BlockVillage.SaveVillagePricingAssumptions;

public record SaveVillagePricingAssumptionsRequest(
    string? LocationMethod = null,
    decimal? CornerAdjustment = null,
    decimal? EdgeAdjustment = null,
    decimal? NearGardenAdjustment = null,
    decimal? OtherAdjustment = null,
    decimal? LandIncreaseDecreaseRate = null,
    decimal? ForceSalePercentage = null,
    List<VillageModelAssumptionData>? ModelAssumptions = null
);
