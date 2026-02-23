namespace Appraisal.Application.Features.PricingAnalysis.GetPricingAnalysisByGroup;

/// <summary>
/// Result of getting pricing analysis by property group
/// </summary>
public record GetPricingAnalysisByGroupResult(
    Guid? Id,
    Guid? PropertyGroupId,
    string? Status,
    decimal? FinalAppraisedValue
);