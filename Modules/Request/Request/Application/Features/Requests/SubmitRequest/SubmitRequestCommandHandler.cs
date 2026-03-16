using Parameter.Contracts.DocumentRequirements;

namespace Request.Application.Features.Requests.SubmitRequest;

internal class SubmitRequestCommandHandler(
    IRequestRepository requestRepository,
    IRequestTitleRepository requestTitleRepository,
    IDocumentChecklistService checklistService,
    IDateTimeProvider dateTimeProvider
) : ICommandHandler<SubmitRequestCommand, SubmitRequestResult>
{
    public async Task<SubmitRequestResult> Handle(SubmitRequestCommand command, CancellationToken cancellationToken)
    {
        var request = await requestRepository.GetByIdWithDocumentsAsync(command.Id, cancellationToken);
        if (request is null) throw new RequestNotFoundException(command.Id);

        // Validate document completeness before submission
        //await ValidateDocumentCompleteness(request, cancellationToken);

        request.Submit(dateTimeProvider.Now);

        return new SubmitRequestResult(true);
    }

    private async Task ValidateDocumentCompleteness(
        Domain.Requests.Request request,
        CancellationToken cancellationToken)
    {
        var purposeCode = request.Purpose;

        // Check application-level documents
        var appRequirements = await checklistService
            .GetApplicationRequirementsAsync(purposeCode, cancellationToken);

        var uploadedAppDocTypes = request.Documents
            .Where(d => d.DocumentId.HasValue)
            .Select(d => d.DocumentType)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingAppDocs = appRequirements
            .Where(r => r.IsRequired && !uploadedAppDocTypes.Contains(r.Code))
            .Select(r => new { r.Code, r.Name })
            .ToList();

        // Check title-level documents
        var titles = (await requestTitleRepository
            .GetByRequestIdWithDocumentsAsync(request.Id, cancellationToken)).ToList();

        var collateralTypeCodes = titles
            .Where(t => !string.IsNullOrWhiteSpace(t.CollateralType))
            .Select(t => t.CollateralType!)
            .Distinct()
            .ToList();

        var collateralRequirements = await checklistService
            .GetCollateralTypeRequirementsAsync(collateralTypeCodes, purposeCode, cancellationToken);

        var collateralReqLookup = collateralRequirements
            .ToDictionary(g => g.CollateralTypeCode, g => g.Documents, StringComparer.OrdinalIgnoreCase);

        var missingTitleDocs = new List<object>();

        foreach (var title in titles)
        {
            if (string.IsNullOrWhiteSpace(title.CollateralType)) continue;
            if (!collateralReqLookup.TryGetValue(title.CollateralType, out var titleReqs)) continue;

            var uploadedTitleDocTypes = title.Documents
                .Where(d => d.DocumentId.HasValue && !string.IsNullOrWhiteSpace(d.DocumentType))
                .Select(d => d.DocumentType!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var missing = titleReqs
                .Where(r => r.IsRequired && !uploadedTitleDocTypes.Contains(r.Code))
                .Select(r => new { r.Code, r.Name })
                .ToList();

            if (missing.Count > 0)
                missingTitleDocs.Add(new
                {
                    TitleId = title.Id,
                    title.CollateralType,
                    Missing = missing
                });
        }

        if (missingAppDocs.Count > 0 || missingTitleDocs.Count > 0)
        {
            var details = new List<string>();

            foreach (var doc in missingAppDocs)
                details.Add($"Application: {doc.Name} ({doc.Code})");

            foreach (var titleGroup in missingTitleDocs)
                details.Add($"Title: {titleGroup}");

            throw new BadRequestException(
                $"Cannot submit: {missingAppDocs.Count + missingTitleDocs.Count} required document(s) missing. " +
                string.Join("; ", details));
        }
    }
}