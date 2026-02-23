namespace Appraisal.Application.Features.PricingAnalysis.GetPricingAnalysis;

/// <summary>
/// Result of getting a pricing analysis
/// </summary>
public record GetPricingAnalysisResult(
    Guid Id,
    Guid PropertyGroupId,
    string Status,
    decimal? FinalAppraisedValue,
    List<ApproachDto> Approaches
);