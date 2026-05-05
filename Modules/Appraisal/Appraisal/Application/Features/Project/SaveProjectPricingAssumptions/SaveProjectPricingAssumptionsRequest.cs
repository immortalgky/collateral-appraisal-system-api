namespace Appraisal.Application.Features.Project.SaveProjectPricingAssumptions;

/// <summary>
/// Request body for saving pricing assumptions on a project.
/// Superset of Condo and LandAndBuilding fields.
/// Condo-only: PoolViewAdjustment, SouthAdjustment, FloorIncrementEveryXFloor, FloorIncrementAmount.
/// LB-only: NearGardenAdjustment, LandIncreaseDecreaseRate.
/// ModelAssumptions: null = no change; empty list = clear all.
/// </summary>
public record SaveProjectPricingAssumptionsRequest(
    string? LocationMethod = null,
    decimal? CornerAdjustment = null,
    decimal? EdgeAdjustment = null,
    decimal? OtherAdjustment = null,
    decimal? ForceSalePercentage = null,
    // Condo-only
    decimal? PoolViewAdjustment = null,
    decimal? SouthAdjustment = null,
    int? FloorIncrementEveryXFloor = null,
    decimal? FloorIncrementAmount = null,
    // LB-only
    decimal? NearGardenAdjustment = null,
    decimal? LandIncreaseDecreaseRate = null,
    // Per-model assumptions (null = no change, empty list = clear)
    List<ProjectModelAssumptionData>? ModelAssumptions = null
);

/// <summary>
/// Per-model assumption data.
/// ProjectModelId links to ProjectModel.Id.
/// StandardLandPrice is LB-only (null for Condo).
/// StandardPrice has been removed — standard price is derived from PricingAnalysis.FinalAppraisedValue.
/// </summary>
public record ProjectModelAssumptionData(
    Guid ProjectModelId,
    string? ModelType = null,
    string? ModelDescription = null,
    decimal? UsableAreaFrom = null,
    decimal? UsableAreaTo = null,
    decimal? StandardLandPrice = null,
    decimal? CoverageAmount = null,
    string? FireInsuranceCondition = null
);
