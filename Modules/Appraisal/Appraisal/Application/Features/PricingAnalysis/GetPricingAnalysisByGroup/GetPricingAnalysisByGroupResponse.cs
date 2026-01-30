namespace Appraisal.Application.Features.PricingAnalysis.GetPricingAnalysisByGroup;

public record GetPricingAnalysisByGroupResponse(
    Guid? Id,
    Guid? PropertyGroupId,
    string? Status,
    decimal? FinalMarketValue,
    decimal? FinalAppraisedValue,
    decimal? FinalForcedSaleValue,
    DateTime? ValuationDate
);
