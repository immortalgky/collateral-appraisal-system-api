namespace Appraisal.Application.Features.PricingAnalysis.GetPricingAnalysisDocuments;

public record GetPricingAnalysisDocumentsResponse(
    List<PricingAnalysisDocumentDto> Documents);
