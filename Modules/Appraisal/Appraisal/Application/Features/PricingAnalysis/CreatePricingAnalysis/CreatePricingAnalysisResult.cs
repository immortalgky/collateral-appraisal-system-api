namespace Appraisal.Application.Features.PricingAnalysis.CreatePricingAnalysis;

/// <summary>
/// Result of creating a PricingAnalysis
/// </summary>
public record CreatePricingAnalysisResult(
    Guid Id,
    string Status
);
