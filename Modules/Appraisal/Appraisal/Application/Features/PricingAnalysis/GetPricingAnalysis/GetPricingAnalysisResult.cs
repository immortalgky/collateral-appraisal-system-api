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
    string? Remark,
    // Group-scoped figures the manual Cost breakdown is derived from: land area comes from the
    // title deeds and the building total from the depreciation schedule, so the client shows the
    // same numbers the server will compute with rather than deriving its own.
    decimal? LandAreaInSqWa,
    decimal? BuildingValue
);
