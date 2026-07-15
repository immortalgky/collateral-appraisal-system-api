using Shared.Time;

namespace Appraisal.Application.Features.PricingAnalysis.AttachPricingAnalysisDocument;

/// <summary>
/// Handler for linking an already-uploaded document to a PricingAnalysis.
/// Mirrors Request module's AttachRequestDocumentCommandHandler.
/// </summary>
public class AttachPricingAnalysisDocumentCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository,
    IDateTimeProvider dateTimeProvider,
    ICurrentUserService currentUser
) : ICommandHandler<AttachPricingAnalysisDocumentCommand, AttachPricingAnalysisDocumentResult>
{
    public async Task<AttachPricingAnalysisDocumentResult> Handle(
        AttachPricingAnalysisDocumentCommand command,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdWithAllDataAsync(
            command.PricingAnalysisId,
            cancellationToken);

        if (pricingAnalysis is null)
            throw new NotFoundException("PricingAnalysis", command.PricingAnalysisId);

        var data = new PricingAnalysisDocumentData(
            command.DocumentId,
            command.FileName,
            null,
            currentUser.Username,
            currentUser.Username,
            dateTimeProvider.Now);

        var document = pricingAnalysis.AddDocument(data);

        return new AttachPricingAnalysisDocumentResult(
            document.Id,
            command.PricingAnalysisId,
            document.DocumentId,
            document.FileName);
    }
}
