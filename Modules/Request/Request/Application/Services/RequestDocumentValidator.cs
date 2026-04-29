using Parameter.Contracts.DocumentRequirements;

namespace Request.Application.Services;

internal class RequestDocumentValidator(
    IDocumentChecklistService checklistService
) : IRequestDocumentValidator
{
    public async Task ValidateAsync(DocumentValidationInput input, CancellationToken cancellationToken)
    {
        // Check application-level documents
        var appRequirements = await checklistService
            .GetApplicationRequirementsAsync(input.Purpose, cancellationToken);

        var uploadedAppDocTypes = input.UploadedApplicationDocTypes
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingAppDocs = appRequirements
            .Where(r => r.IsRequired && !uploadedAppDocTypes.Contains(r.Code))
            .Select(r => new { r.Code, r.Name })
            .ToList();

        // Check title-level documents
        var collateralTypeCodes = input.Titles
            .Where(t => !string.IsNullOrWhiteSpace(t.CollateralType))
            .Select(t => t.CollateralType!)
            .Distinct()
            .ToList();

        var collateralRequirements = await checklistService
            .GetCollateralTypeRequirementsAsync(collateralTypeCodes, input.Purpose, cancellationToken);

        var collateralReqLookup = collateralRequirements
            .ToDictionary(g => g.CollateralTypeCode, g => g.Documents, StringComparer.OrdinalIgnoreCase);

        var missingTitleDetails = new List<string>();

        foreach (var title in input.Titles)
        {
            if (string.IsNullOrWhiteSpace(title.CollateralType)) continue;
            if (!collateralReqLookup.TryGetValue(title.CollateralType, out var titleReqs)) continue;

            var uploadedTitleDocTypes = title.UploadedDocTypes
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var missing = titleReqs
                .Where(r => r.IsRequired && !uploadedTitleDocTypes.Contains(r.Code))
                .ToList();

            foreach (var doc in missing)
                missingTitleDetails.Add($"Title [{title.CollateralType}]: {doc.Name} ({doc.Code})");
        }

        if (missingAppDocs.Count > 0 || missingTitleDetails.Count > 0)
        {
            var details = new List<string>();

            foreach (var doc in missingAppDocs)
                details.Add($"Application: {doc.Name} ({doc.Code})");

            details.AddRange(missingTitleDetails);

            throw new BadRequestException(
                $"Cannot submit: {missingAppDocs.Count + missingTitleDetails.Count} required document(s) missing. " +
                string.Join("; ", details));
        }
    }
}
