using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.PricingAnalysis.UpdatePricingAnalysisDocument;

/// <summary>
/// Command to replace the linked document on an existing PricingAnalysisDocument entry.
/// Fires DocumentLinkedEvent / DocumentUpdatedEvent / DocumentUnlinkedEvent depending on the
/// previous/new DocumentId combination (see PricingAnalysis.UpdateDocument).
/// </summary>
public record UpdatePricingAnalysisDocumentCommand(
    Guid PricingAnalysisId,
    Guid DocumentEntryId,
    Guid? DocumentId,
    string? FileName
) : ICommand<UpdatePricingAnalysisDocumentResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
