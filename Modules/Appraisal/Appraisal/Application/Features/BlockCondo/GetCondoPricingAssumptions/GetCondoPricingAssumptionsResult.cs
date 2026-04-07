namespace Appraisal.Application.Features.BlockCondo.GetCondoPricingAssumptions;

public record GetCondoPricingAssumptionsResult(CondoPricingAssumptionDto? Assumption);

public record CondoPricingAssumptionDto(
    Guid Id,
    Guid AppraisalId,
    string? LocationMethod,
    decimal? CornerAdjustment,
    decimal? EdgeAdjustment,
    decimal? PoolViewAdjustment,
    decimal? SouthAdjustment,
    decimal? OtherAdjustment,
    int? FloorIncrementEveryXFloor,
    decimal? FloorIncrementAmount,
    decimal? ForceSalePercentage,
    IReadOnlyList<CondoModelAssumptionDto> ModelAssumptions
);

public record CondoModelAssumptionDto(
    Guid CondoModelId,
    string? ModelType,
    string? ModelDescription,
    decimal? UsableAreaFrom,
    decimal? UsableAreaTo,
    decimal? StandardPrice,
    decimal? CoverageAmount,
    string? FireInsuranceCondition
);
