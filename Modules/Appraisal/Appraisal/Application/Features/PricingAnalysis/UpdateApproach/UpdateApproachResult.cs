namespace Appraisal.Application.Features.PricingAnalysis.UpdateApproach;

/// <summary>
/// Result of updating an approach
/// </summary>
public record UpdateApproachResult(
    Guid Id,
    string ApproachType,
    decimal? ApproachValue
);