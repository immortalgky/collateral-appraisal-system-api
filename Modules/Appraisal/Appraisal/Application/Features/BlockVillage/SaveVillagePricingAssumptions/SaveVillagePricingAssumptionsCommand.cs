using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.BlockVillage.SaveVillagePricingAssumptions;

public record SaveVillagePricingAssumptionsCommand(
    Guid AppraisalId,
    string? LocationMethod = null,
    decimal? CornerAdjustment = null,
    decimal? EdgeAdjustment = null,
    decimal? NearGardenAdjustment = null,
    decimal? OtherAdjustment = null,
    decimal? LandIncreaseDecreaseRate = null,
    decimal? ForceSalePercentage = null,
    List<VillageModelAssumptionData>? ModelAssumptions = null
) : ICommand<SaveVillagePricingAssumptionsResult>, ITransactionalCommand<IAppraisalUnitOfWork>;

public record VillageModelAssumptionData(
    Guid VillageModelId,
    string? ModelType = null,
    string? ModelDescription = null,
    decimal? UsableAreaFrom = null,
    decimal? UsableAreaTo = null,
    decimal? StandardLandPrice = null,
    decimal? StandardPrice = null,
    decimal? CoverageAmount = null,
    string? FireInsuranceCondition = null
);
