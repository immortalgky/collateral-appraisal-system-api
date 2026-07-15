using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.PricingAnalysis.RemovePricingAnalysisDocument;

/// <summary>
/// Command to remove a PricingAnalysisDocument entry entirely (not just unlink the file).
/// Fires DocumentUnlinkedEvent if a document was linked.
/// </summary>
public record RemovePricingAnalysisDocumentCommand(
    Guid PricingAnalysisId,
    Guid DocumentEntryId
) : ICommand<RemovePricingAnalysisDocumentResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
