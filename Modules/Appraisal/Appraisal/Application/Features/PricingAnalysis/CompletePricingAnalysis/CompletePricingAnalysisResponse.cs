namespace Appraisal.Application.Features.PricingAnalysis.CompletePricingAnalysis;

public record CompletePricingAnalysisResponse(
    Guid Id,
    string Status,
    DateTime? ValuationDate
);
