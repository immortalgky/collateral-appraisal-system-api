using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.SaveLeaseholdAnalysis;

public record SaveLeaseholdAnalysisCommand(
    Guid PricingAnalysisId,
    Guid MethodId,
    decimal LandValuePerSqWa,
    string LandGrowthRateType,
    decimal LandGrowthRatePercent,
    int LandGrowthIntervalYears,
    decimal ConstructionCostIndex,
    decimal InitialBuildingValue,
    decimal DepreciationRate,
    int DepreciationIntervalYears,
    int BuildingCalcStartYear,
    decimal DiscountRate,
    IReadOnlyList<LandGrowthPeriodInput>? LandGrowthPeriods,
    bool IsPartialUsage,
    decimal? PartialRai,
    decimal? PartialNgan,
    decimal? PartialWa,
    decimal? PricePerSqWa,
    string? Remark,
    decimal? EstimatePriceRounded
) : ICommand<SaveLeaseholdAnalysisResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
