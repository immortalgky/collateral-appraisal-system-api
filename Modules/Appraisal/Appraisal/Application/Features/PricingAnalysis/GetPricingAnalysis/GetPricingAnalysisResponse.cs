using Appraisal.Application.Features.PricingAnalysis.GetPricingAnalysisDocuments;

namespace Appraisal.Application.Features.PricingAnalysis.GetPricingAnalysis;

public record GetPricingAnalysisResponse(
    Guid Id,
    PricingAnalysisSubjectType SubjectType,
    Guid? AnchorId,
    string? AnchorRefKey,
    Guid? HostMethodId,
    string Status,
    decimal? FinalMarketValue,
    decimal? FinalAppraisedValue,
    decimal? FinalForcedSaleValue,
    DateTime? ValuationDate,
    bool UseSystemCalc,
    List<ApproachDto> Approaches,
    List<PricingAnalysisDocumentDto> Documents,
    string? Remark
);
