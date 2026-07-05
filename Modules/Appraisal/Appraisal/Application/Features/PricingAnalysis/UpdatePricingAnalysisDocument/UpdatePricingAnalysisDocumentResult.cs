namespace Appraisal.Application.Features.PricingAnalysis.UpdatePricingAnalysisDocument;

public record UpdatePricingAnalysisDocumentResult(
    Guid Id,
    Guid PricingAnalysisId,
    Guid? DocumentId,
    string? FileName);
