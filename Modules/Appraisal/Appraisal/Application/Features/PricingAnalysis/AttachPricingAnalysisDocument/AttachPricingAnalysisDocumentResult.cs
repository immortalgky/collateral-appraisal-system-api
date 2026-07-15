namespace Appraisal.Application.Features.PricingAnalysis.AttachPricingAnalysisDocument;

public record AttachPricingAnalysisDocumentResult(
    Guid Id,
    Guid PricingAnalysisId,
    Guid? DocumentId,
    string? FileName);
