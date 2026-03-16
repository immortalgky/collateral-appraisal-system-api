using Parameter.DocumentRequirements.Models;

namespace Parameter.DocumentRequirements.Features.GetDocumentChecklist;

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
        var universalRequirements = await _repository.GetUniversalRequirementsAsync(cancellationToken);

        var applicationDocuments = universalRequirements
            .Select(MapToDto)
            .ToList();

        if (!string.IsNullOrWhiteSpace(query.PurposeCode))
        {
            var purposeOnlyRequirements = await _repository
                .GetPurposeOnlyRequirementsAsync(query.PurposeCode, cancellationToken);

            var existingDocTypeIds = new HashSet<Guid>(applicationDocuments.Select(d => d.DocumentTypeId));

            foreach (var req in purposeOnlyRequirements)
            {
                if (existingDocTypeIds.Contains(req.DocumentTypeId))
                {
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
            var propertyRequirements = await _repository
                .GetRequirementsByPropertyTypesAsync(propertyTypeCodes, query.PurposeCode, cancellationToken);

            var groupedRequirements = propertyRequirements
                .GroupBy(r => r.PropertyTypeCode!)
                .OrderBy(g => propertyTypeCodes.IndexOf(g.Key));

            foreach (var group in groupedRequirements)
            {
                var propertyTypeCode = group.Key;
                var propertyTypeName = GetPropertyTypeName(propertyTypeCode);

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
