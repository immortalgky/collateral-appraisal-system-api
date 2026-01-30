namespace Appraisal.Application.Features.PricingAnalysis.UpdatePricingAnalysis;

public record UpdatePricingAnalysisRequest(
    decimal MarketValue,
    decimal AppraisedValue,
    decimal? ForcedSaleValue
);
