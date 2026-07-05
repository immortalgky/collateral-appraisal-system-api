namespace Appraisal.Application.Features.PricingAnalysis.GetPricingAnalysisDocuments;

public class GetPricingAnalysisDocumentsQueryHandler(
    IPricingAnalysisRepository pricingAnalysisRepository
) : IQueryHandler<GetPricingAnalysisDocumentsQuery, GetPricingAnalysisDocumentsResult>
{
    public async Task<GetPricingAnalysisDocumentsResult> Handle(
        GetPricingAnalysisDocumentsQuery query,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdWithAllDataAsync(
            query.PricingAnalysisId,
            cancellationToken);

        if (pricingAnalysis is null)
            throw new NotFoundException("PricingAnalysis", query.PricingAnalysisId);

        var documents = pricingAnalysis.Documents
            .Select(d => new PricingAnalysisDocumentDto(
                d.Id,
                d.DocumentId,
                d.FileName,
                d.FilePath,
                d.UploadedBy,
                d.UploadedByName,
                d.UploadedAt))
            .ToList();

        return new GetPricingAnalysisDocumentsResult(documents);
    }
}

public record PricingAnalysisDocumentDto(
    Guid Id,
    Guid? DocumentId,
    string? FileName,
    string? FilePath,
    string? UploadedBy,
    string? UploadedByName,
    DateTime? UploadedAt);
