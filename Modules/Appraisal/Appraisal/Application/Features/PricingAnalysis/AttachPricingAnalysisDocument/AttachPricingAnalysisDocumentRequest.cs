namespace Appraisal.Application.Features.PricingAnalysis.AttachPricingAnalysisDocument;

/// <summary>
/// Request body for attaching an already-uploaded document (via the Document module's
/// POST /documents endpoint) to a PricingAnalysis.
/// </summary>
public record AttachPricingAnalysisDocumentRequest(
    Guid DocumentId,
    string? FileName);
