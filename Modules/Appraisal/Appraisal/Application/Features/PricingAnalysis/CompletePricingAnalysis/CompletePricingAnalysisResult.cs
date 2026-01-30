namespace Appraisal.Application.Features.PricingAnalysis.CompletePricingAnalysis;

/// <summary>
/// Result of completing pricing analysis
/// </summary>
public record CompletePricingAnalysisResult(
    Guid Id,
    string Status
);