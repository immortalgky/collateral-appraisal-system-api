namespace Appraisal.Application.Features.PricingAnalysis.UpdatePricingAnalysisDocument;

public record UpdatePricingAnalysisDocumentResponse(
    Guid Id,
    Guid PricingAnalysisId,
    Guid? DocumentId,
    string? FileName);
