namespace Appraisal.Application.Features.PricingAnalysis.CompletePricingAnalysis;

public record CompletePricingAnalysisRequest(
    decimal MarketValue,
    decimal AppraisedValue,
    decimal? ForcedSaleValue
);
