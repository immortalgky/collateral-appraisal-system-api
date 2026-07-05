namespace Appraisal.Application.Features.PricingAnalysis.GetPricingAnalysisDocuments;

/// <summary>
/// Query to list all document entries attached to a pricing analysis.
/// </summary>
public record GetPricingAnalysisDocumentsQuery(
    Guid PricingAnalysisId
) : IQuery<GetPricingAnalysisDocumentsResult>;
