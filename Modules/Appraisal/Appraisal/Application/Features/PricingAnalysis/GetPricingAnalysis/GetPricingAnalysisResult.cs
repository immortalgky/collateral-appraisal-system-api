namespace Appraisal.Application.Features.PricingAnalysis.GetPricingAnalysis;

/// <summary>
/// Result of getting a pricing analysis
/// </summary>
public record GetPricingAnalysisResult(
    Guid Id,
    Guid? PropertyGroupId,
    Guid? ProjectModelId,
    string Status,
    decimal? FinalAppraisedValue,
    bool UseSystemCalc,
    List<ApproachDto> Approaches
);