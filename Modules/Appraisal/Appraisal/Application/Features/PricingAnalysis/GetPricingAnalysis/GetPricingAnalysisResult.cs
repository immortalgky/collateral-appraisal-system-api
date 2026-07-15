using Appraisal.Application.Features.PricingAnalysis.GetPricingAnalysisDocuments;

namespace Appraisal.Application.Features.PricingAnalysis.GetPricingAnalysis;

/// <summary>
/// Result of getting a pricing analysis
/// </summary>
public record GetPricingAnalysisResult(
    Guid Id,
    PricingAnalysisSubjectType SubjectType,
    Guid? AnchorId,
    string? AnchorRefKey,
    Guid? HostMethodId,
    string Status,
    decimal? FinalAppraisedValue,
    bool UseSystemCalc,
    List<ApproachDto> Approaches,
    List<PricingAnalysisDocumentDto> Documents,
    string? Remark
);
