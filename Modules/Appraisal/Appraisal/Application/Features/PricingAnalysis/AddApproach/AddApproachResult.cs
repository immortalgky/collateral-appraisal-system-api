namespace Appraisal.Application.Features.PricingAnalysis.AddApproach;

/// <summary>
/// Result of adding an approach to a pricing analysis
/// </summary>
public record AddApproachResult(
    Guid Id,
    string ApproachType
);