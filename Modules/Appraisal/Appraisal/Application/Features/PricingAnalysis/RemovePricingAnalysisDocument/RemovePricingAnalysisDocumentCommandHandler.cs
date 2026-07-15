namespace Appraisal.Application.Features.PricingAnalysis.RemovePricingAnalysisDocument;

public class RemovePricingAnalysisDocumentCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository
) : ICommandHandler<RemovePricingAnalysisDocumentCommand, RemovePricingAnalysisDocumentResult>
{
    public async Task<RemovePricingAnalysisDocumentResult> Handle(
        RemovePricingAnalysisDocumentCommand command,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdWithAllDataAsync(
            command.PricingAnalysisId,
            cancellationToken);

        if (pricingAnalysis is null)
            throw new NotFoundException("PricingAnalysis", command.PricingAnalysisId);

        pricingAnalysis.RemoveDocument(command.DocumentEntryId);

        return new RemovePricingAnalysisDocumentResult(command.DocumentEntryId, true);
    }
}
