using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Project.SaveProjectPricingAssumptions;

/// <summary>Command to save pricing assumptions for a project (Condo or LandAndBuilding).</summary>
public record SaveProjectPricingAssumptionsCommand(
    Guid AppraisalId,
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
    // Per-model assumptions (null = no change)
    List<ProjectModelAssumptionData>? ModelAssumptions = null
) : ICommand<SaveProjectPricingAssumptionsResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
