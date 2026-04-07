namespace Appraisal.Application.Features.BlockCondo.SaveCondoPricingAssumptions;

public record SaveCondoPricingAssumptionsRequest(
    string? LocationMethod = null,
    decimal? CornerAdjustment = null,
    decimal? EdgeAdjustment = null,
    decimal? PoolViewAdjustment = null,
    decimal? SouthAdjustment = null,
    decimal? OtherAdjustment = null,
    int? FloorIncrementEveryXFloor = null,
    decimal? FloorIncrementAmount = null,
    decimal? ForceSalePercentage = null,
    List<CondoModelAssumptionData>? ModelAssumptions = null
);
