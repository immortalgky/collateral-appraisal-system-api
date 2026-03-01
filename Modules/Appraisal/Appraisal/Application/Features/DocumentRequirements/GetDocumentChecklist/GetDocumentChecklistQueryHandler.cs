namespace Appraisal.Application.Features.DocumentRequirements.GetDocumentChecklist;

/// <summary>
/// Handler for GetDocumentChecklistQuery.
/// Assembles a checklist from 4 tiers of requirements:
/// - Tier 1 (Universal): PropertyType=NULL, Purpose=NULL
/// - Tier 2 (Purpose-only): PropertyType=NULL, Purpose=X
/// - Tier 3 (PropertyType-only): PropertyType=X, Purpose=NULL
/// - Tier 4 (Fully specific): PropertyType=X, Purpose=X
/// </summary>
public class GetDocumentChecklistQueryHandler : IQueryHandler<GetDocumentChecklistQuery, GetDocumentChecklistResult>
{
    private readonly IDocumentRequirementRepository _repository;

    private static readonly Dictionary<string, string> PropertyTypeNames = new(StringComparer.OrdinalIgnoreCase)
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
        // === Application Documents (Tier 1 + Tier 2) ===
        // Tier 1: Universal (PropertyType=NULL, Purpose=NULL)
        var universalRequirements = await _repository.GetUniversalRequirementsAsync(cancellationToken);

        var applicationDocuments = universalRequirements
            .Select(MapToDto)
            .ToList();

        // Tier 2: Purpose-only (PropertyType=NULL, Purpose=X)
        if (!string.IsNullOrWhiteSpace(query.PurposeCode))
        {
            var purposeOnlyRequirements = await _repository
                .GetPurposeOnlyRequirementsAsync(query.PurposeCode, cancellationToken);

            // Merge: purpose-only takes priority over universal if same DocumentTypeId
            var existingDocTypeIds = new HashSet<Guid>(applicationDocuments.Select(d => d.DocumentTypeId));

            foreach (var req in purposeOnlyRequirements)
            {
                if (existingDocTypeIds.Contains(req.DocumentTypeId))
                {
                    // Replace universal with purpose-specific
                    var index = applicationDocuments.FindIndex(d => d.DocumentTypeId == req.DocumentTypeId);
                    applicationDocuments[index] = MapToDto(req);
                }
                else
                {
                    applicationDocuments.Add(MapToDto(req));
                    existingDocTypeIds.Add(req.DocumentTypeId);
                }
            }
        }

        // === Property Type Groups (Tier 3 + Tier 4) ===
        var propertyTypeCodes = query.PropertyTypeCodes
            .Select(c => c.ToUpperInvariant())
            .Distinct()
            .ToList();

        var propertyTypeGroups = new List<PropertyTypeDocumentGroupDto>();

        if (propertyTypeCodes.Count != 0)
        {
            // Fetch Tier 3 (PurposeCode=NULL) and Tier 4 (PurposeCode=X) in one query
            var propertyRequirements = await _repository
                .GetRequirementsByPropertyTypesAsync(propertyTypeCodes, query.PurposeCode, cancellationToken);

            // Group by property type code
            var groupedRequirements = propertyRequirements
                .GroupBy(r => r.PropertyTypeCode!)
                .OrderBy(g => propertyTypeCodes.IndexOf(g.Key));

            foreach (var group in groupedRequirements)
            {
                var propertyTypeCode = group.Key;
                var propertyTypeName = GetPropertyTypeName(propertyTypeCode);

                // Dedup by DocumentTypeId: Tier 4 takes priority over Tier 3
                var documents = group
                    .GroupBy(r => r.DocumentTypeId)
                    .Select(g => g.OrderByDescending(r => r.PurposeCode is not null ? 1 : 0).First())
                    .Select(MapToDto)
                    .ToList();

                propertyTypeGroups.Add(new PropertyTypeDocumentGroupDto(
                    propertyTypeCode,
                    propertyTypeName,
                    documents));
            }
        }

        return new GetDocumentChecklistResult(applicationDocuments, propertyTypeGroups);
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

    private static string GetPropertyTypeName(string code)
    {
        return PropertyTypeNames.TryGetValue(code, out var name) ? name : code;
    }
}
