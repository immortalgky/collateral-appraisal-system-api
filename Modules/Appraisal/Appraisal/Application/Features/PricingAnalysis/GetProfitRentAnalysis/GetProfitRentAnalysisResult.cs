namespace Appraisal.Application.Features.PricingAnalysis.GetProfitRentAnalysis;

public record GetProfitRentAnalysisResult(ProfitRentAnalysisDto? Analysis, string? Remark);

public record ProfitRentAnalysisDto(
    Guid Id,
    Guid PricingMethodId,
    decimal MarketRentalFeePerSqWa,
    string GrowthRateType,
    decimal GrowthRatePercent,
    int GrowthIntervalYears,
    decimal DiscountRate,
    bool IncludeBuildingCost,
    decimal TotalMarketRentalFee,
    decimal TotalContractRentalFee,
    decimal TotalReturnsFromLease,
    decimal TotalPresentValue,
    decimal FinalValueRounded,
    decimal? EstimatePriceRounded,
    decimal? TotalBuildingCost,
    decimal? AppraisalPriceWithBuilding,
    decimal? AppraisalPriceWithBuildingRounded,
    List<ProfitRentGrowthPeriodDto> GrowthPeriods,
    List<ProfitRentCalculationDetailDto> CalculationDetails
);

public record ProfitRentGrowthPeriodDto(
    Guid Id,
    int FromYear,
    int ToYear,
    decimal GrowthRatePercent
);

public record ProfitRentCalculationDetailDto(
    decimal Year,
    decimal NumberOfMonths,
    decimal MarketRentalFeePerSqWa,
    decimal MarketRentalFeeGrowthPercent,
    decimal MarketRentalFeePerMonth,
    decimal MarketRentalFeePerYear,
    decimal ContractRentalFeePerYear,
    decimal ReturnsFromLease,
    decimal PvFactor,
    decimal PresentValue
);
