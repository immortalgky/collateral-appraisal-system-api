namespace Appraisal.Application.Features.Project.GetProjectPricingAssumptions;

/// <summary>
/// Unified pricing assumption DTO covering both Condo and LandAndBuilding fields.
/// Condo-only: PoolViewAdjustment, SouthAdjustment, FloorIncrementEveryXFloor, FloorIncrementAmount.
/// LB-only: NearGardenAdjustment, LandIncreaseDecreaseRate.
/// ProjectType indicates which type-specific fields are populated.
/// </summary>
public record ProjectPricingAssumptionDto(
    Guid Id,
    Guid ProjectId,
    string ProjectType,
    string? LocationMethod,
    // Common adjustments
    decimal? CornerAdjustment,
    decimal? EdgeAdjustment,
    decimal? OtherAdjustment,
    decimal? ForceSalePercentage,
    // Condo-only
    decimal? PoolViewAdjustment,
    decimal? SouthAdjustment,
    int? FloorIncrementEveryXFloor,
    decimal? FloorIncrementAmount,
    // LB-only
    decimal? NearGardenAdjustment,
    decimal? LandIncreaseDecreaseRate,
    // Per-model assumptions
    List<ProjectModelAssumptionDto> ModelAssumptions
);

/// <summary>
/// Per-model assumption DTO.
/// StandardLandPrice is LB-only (null for Condo).
/// </summary>
public record ProjectModelAssumptionDto(
    Guid ProjectModelId,
    string? ModelType,
    string? ModelDescription,
    decimal? UsableAreaFrom,
    decimal? UsableAreaTo,
    decimal? StandardPrice,
    decimal? StandardLandPrice,
    decimal? CoverageAmount,
    string? FireInsuranceCondition
);
