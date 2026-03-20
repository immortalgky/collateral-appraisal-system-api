using Parameter.Contracts.DocumentRequirements;

namespace Request.Application.Features.Requests.GetRequestDocumentChecklist;

internal class GetRequestDocumentChecklistQueryHandler(
    IRequestRepository requestRepository,
    IRequestTitleRepository requestTitleRepository,
    IDocumentChecklistService checklistService
) : IQueryHandler<GetRequestDocumentChecklistQuery, GetRequestDocumentChecklistResult>
{
    public async Task<GetRequestDocumentChecklistResult> Handle(
        GetRequestDocumentChecklistQuery query,
        CancellationToken cancellationToken)
    {
        // 1. Load request with documents
        var request = await requestRepository.GetByIdWithDocumentsAsync(query.RequestId, cancellationToken);
        if (request is null) throw new RequestNotFoundException(query.RequestId);

        // 2. Load titles with documents
        var titles = (await requestTitleRepository
            .GetByRequestIdWithDocumentsAsync(query.RequestId, cancellationToken)).ToList();

        // 3. Get purpose code from request
        var purposeCode = request.Purpose;

        // 4. Get distinct collateral type codes
        var collateralTypeCodes = titles
            .Where(t => !string.IsNullOrWhiteSpace(t.CollateralType))
            .Select(t => t.CollateralType!)
            .Distinct()
            .ToList();

        // 5. Get application-level requirements
        var appRequirements = await checklistService
            .GetApplicationRequirementsAsync(purposeCode, cancellationToken);

        // 6. Get collateral-type-specific requirements
        var collateralRequirements = await checklistService
            .GetCollateralTypeRequirementsAsync(collateralTypeCodes, purposeCode, cancellationToken);

        // 7. Cross-reference application documents
        var uploadedAppDocTypes = request.Documents
            .Where(d => d.DocumentId.HasValue)
            .Select(d => d.DocumentType)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var applicationDocuments = appRequirements
            .Select(r => new ApplicationDocumentChecklistItem(
                r.Code,
                r.Name,
                r.Category,
                r.IsRequired,
                uploadedAppDocTypes.Contains(r.Code),
                r.Notes))
            .ToList();

        // 8. Cross-reference per-title documents
        var collateralReqLookup = collateralRequirements
            .ToDictionary(g => g.CollateralTypeCode, g => g.Documents, StringComparer.OrdinalIgnoreCase);

        var titleDocuments = new List<TitleDocumentChecklistGroup>();

        foreach (var title in titles)
        {
            if (string.IsNullOrWhiteSpace(title.CollateralType)) continue;

            if (!collateralReqLookup.TryGetValue(title.CollateralType, out var titleReqs))
                continue;

            var uploadedTitleDocTypes = title.Documents
                .Where(d => d.DocumentId.HasValue && !string.IsNullOrWhiteSpace(d.DocumentType))
                .Select(d => d.DocumentType!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var docs = titleReqs
                .Select(r => new TitleDocumentChecklistItem(
                    r.Code,
                    r.Name,
                    r.Category,
                    r.IsRequired,
                    uploadedTitleDocTypes.Contains(r.Code),
                    r.Notes))
                .ToList();

            titleDocuments.Add(new TitleDocumentChecklistGroup(
                title.Id,
                title.CollateralType,
                null, // OwnerName can be enriched later
                docs));
        }

        // 9. Calculate completeness
        var missingRequired = applicationDocuments.Count(d => d.IsRequired && !d.IsUploaded)
                              + titleDocuments.SelectMany(t => t.Documents).Count(d => d.IsRequired && !d.IsUploaded);

        return new GetRequestDocumentChecklistResult(
            applicationDocuments,
            titleDocuments,
            missingRequired == 0,
            missingRequired);
    }
}
