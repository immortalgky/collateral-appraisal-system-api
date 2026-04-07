using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.SaveProfitRentAnalysis;

public record SaveProfitRentAnalysisCommand(
    Guid PricingAnalysisId,
    Guid MethodId,
    decimal MarketRentalFeePerSqWa,
    string GrowthRateType,
    decimal GrowthRatePercent,
    int GrowthIntervalYears,
    decimal DiscountRate,
    bool IncludeBuildingCost,
    IReadOnlyList<ProfitRentGrowthPeriodInput>? GrowthPeriods,
    string? Remark,
    decimal? EstimatePriceRounded,
    decimal? AppraisalPriceWithBuildingRounded
) : ICommand<SaveProfitRentAnalysisResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
