namespace Appraisal.Application.Features.PricingAnalysis.SaveProfitRentAnalysis;

public record SaveProfitRentAnalysisResult(
    Guid PricingAnalysisId,
    Guid MethodId,
    decimal TotalMarketRentalFee,
    decimal TotalContractRentalFee,
    decimal TotalReturnsFromLease,
    decimal TotalPresentValue,
    decimal FinalValueRounded,
    decimal? TotalBuildingCost,
    decimal? AppraisalPriceWithBuilding,
    decimal? AppraisalPriceWithBuildingRounded
);
