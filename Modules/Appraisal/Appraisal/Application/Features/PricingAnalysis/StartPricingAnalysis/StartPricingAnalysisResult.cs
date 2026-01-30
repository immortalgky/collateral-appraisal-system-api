namespace Appraisal.Application.Features.PricingAnalysis.StartPricingAnalysis;

/// <summary>
/// Result of starting pricing analysis
/// </summary>
public record StartPricingAnalysisResult(
    Guid Id,
    string Status
);
