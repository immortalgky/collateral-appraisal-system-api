namespace Appraisal.Application.Features.PricingAnalysis.CreateProjectModelPricingAnalysis;

/// <summary>
/// Result of creating a PricingAnalysis for a ProjectModel.
/// </summary>
public record CreateProjectModelPricingAnalysisResult(
    Guid Id,
    string Status
);
