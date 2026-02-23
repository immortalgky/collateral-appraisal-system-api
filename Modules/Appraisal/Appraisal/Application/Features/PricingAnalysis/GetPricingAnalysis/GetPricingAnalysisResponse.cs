namespace Appraisal.Application.Features.PricingAnalysis.GetPricingAnalysis;

public record GetPricingAnalysisResponse(
    Guid Id,
    Guid PropertyGroupId,
    string Status,
    decimal? FinalMarketValue,
    decimal? FinalAppraisedValue,
    decimal? FinalForcedSaleValue,
    DateTime? ValuationDate,
    List<ApproachDto> Approaches
);
