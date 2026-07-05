using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.PricingAnalysis.AttachPricingAnalysisDocument;

/// <summary>
/// Command to link an already-uploaded document (from the Document module) to a PricingAnalysis.
/// </summary>
public record AttachPricingAnalysisDocumentCommand(
    Guid PricingAnalysisId,
    Guid DocumentId,
    string? FileName
) : ICommand<AttachPricingAnalysisDocumentResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
