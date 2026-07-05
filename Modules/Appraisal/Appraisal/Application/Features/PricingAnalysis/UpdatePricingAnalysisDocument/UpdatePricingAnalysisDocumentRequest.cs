namespace Appraisal.Application.Features.PricingAnalysis.UpdatePricingAnalysisDocument;

/// <summary>
/// Request body for replacing the linked document on an existing PricingAnalysisDocument entry.
/// Pass DocumentId = null to unlink without deleting the entry.
/// </summary>
public record UpdatePricingAnalysisDocumentRequest(
    Guid? DocumentId,
    string? FileName);
