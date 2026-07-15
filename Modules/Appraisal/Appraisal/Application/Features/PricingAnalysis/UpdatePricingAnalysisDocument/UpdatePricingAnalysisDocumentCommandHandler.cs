using Shared.Time;

namespace Appraisal.Application.Features.PricingAnalysis.UpdatePricingAnalysisDocument;

public class UpdatePricingAnalysisDocumentCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository,
    IDateTimeProvider dateTimeProvider,
    ICurrentUserService currentUser
) : ICommandHandler<UpdatePricingAnalysisDocumentCommand, UpdatePricingAnalysisDocumentResult>
{
    public async Task<UpdatePricingAnalysisDocumentResult> Handle(
        UpdatePricingAnalysisDocumentCommand command,
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

        pricingAnalysis.UpdateDocument(command.DocumentEntryId, data);

        var document = pricingAnalysis.GetDocument(command.DocumentEntryId)!;

        return new UpdatePricingAnalysisDocumentResult(
            document.Id,
            command.PricingAnalysisId,
            document.DocumentId,
            document.FileName);
    }
}
