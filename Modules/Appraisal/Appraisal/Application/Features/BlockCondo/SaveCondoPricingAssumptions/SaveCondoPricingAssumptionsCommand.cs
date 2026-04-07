using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.BlockCondo.SaveCondoPricingAssumptions;

public record SaveCondoPricingAssumptionsCommand(
    Guid AppraisalId,
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
) : ICommand<SaveCondoPricingAssumptionsResult>, ITransactionalCommand<IAppraisalUnitOfWork>;

public record CondoModelAssumptionData(
    Guid CondoModelId,
    string? ModelType = null,
    string? ModelDescription = null,
    decimal? UsableAreaFrom = null,
    decimal? UsableAreaTo = null,
    decimal? StandardPrice = null,
    decimal? CoverageAmount = null,
    string? FireInsuranceCondition = null
);
