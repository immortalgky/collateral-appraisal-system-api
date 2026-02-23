namespace Appraisal.Application.Features.DocumentRequirements.GetDocumentChecklist;

/// <summary>
/// Handler for GetDocumentChecklistQuery
/// </summary>
public class GetDocumentChecklistQueryHandler : IQueryHandler<GetDocumentChecklistQuery, GetDocumentChecklistResult>
{
    private readonly IDocumentRequirementRepository _repository;

    // Mapping of collateral type codes to display names
    private static readonly Dictionary<string, string> CollateralTypeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["L"] = "Land",
        ["B"] = "Building",
        ["LB"] = "Land and Building",
        ["U"] = "Condo",
        ["LSL"] = "Lease Agreement Land",
        ["LSB"] = "Lease Agreement Building",
        ["LS"] = "Lease Agreement Land and Building",
        ["LSU"] = "Lease Agreement Condo",
        ["MAC"] = "Machine",
        ["VEH"] = "Vehicle",
        ["VES"] = "Vessel"
    };

    public GetDocumentChecklistQueryHandler(IDocumentRequirementRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetDocumentChecklistResult> Handle(
        GetDocumentChecklistQuery query,
        CancellationToken cancellationToken)
    {
        // Get application-level documents (CollateralTypeCode IS NULL)
        var applicationRequirements = await _repository.GetApplicationLevelRequirementsAsync(cancellationToken);

        var applicationDocuments = applicationRequirements
            .Select(MapToDto)
            .ToList();

        // Get collateral-specific documents
        var collateralTypeCodes = query.CollateralTypeCodes
            .Select(c => c.ToUpperInvariant())
            .Distinct()
            .ToList();

        var collateralGroups = new List<CollateralDocumentGroupDto>();

        if (collateralTypeCodes.Count != 0)
        {
            var collateralRequirements = await _repository
                .GetRequirementsByCollateralTypesAsync(collateralTypeCodes, cancellationToken);

            // Group by collateral type
            var groupedRequirements = collateralRequirements
                .GroupBy(r => r.CollateralTypeCode!)
                .OrderBy(g => collateralTypeCodes.IndexOf(g.Key)); // Maintain order from request

            foreach (var group in groupedRequirements)
            {
                var collateralTypeCode = group.Key;
                var collateralTypeName = GetCollateralTypeName(collateralTypeCode);

                var documents = group
                    .Select(MapToDto)
                    .ToList();

                collateralGroups.Add(new CollateralDocumentGroupDto(
                    collateralTypeCode,
                    collateralTypeName,
                    documents));
            }
        }

        return new GetDocumentChecklistResult(applicationDocuments, collateralGroups);
    }

    private static DocumentChecklistItemDto MapToDto(DocumentRequirement requirement)
    {
        return new DocumentChecklistItemDto
        {
            DocumentTypeId = requirement.DocumentTypeId,
            Code = requirement.DocumentType.Code,
            Name = requirement.DocumentType.Name,
            Category = requirement.DocumentType.Category,
            IsRequired = requirement.IsRequired,
            Notes = requirement.Notes
        };
    }

    private static string GetCollateralTypeName(string code)
    {
        return CollateralTypeNames.TryGetValue(code, out var name) ? name : code;
    }
}
