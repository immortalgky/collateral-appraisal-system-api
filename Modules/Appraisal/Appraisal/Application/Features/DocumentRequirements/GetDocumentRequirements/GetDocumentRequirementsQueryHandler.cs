namespace Appraisal.Application.Features.DocumentRequirements.GetDocumentRequirements;

/// <summary>
/// Handler for GetDocumentRequirementsQuery
/// </summary>
public class
    GetDocumentRequirementsQueryHandler : IQueryHandler<GetDocumentRequirementsQuery, GetDocumentRequirementsResult>
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

    public GetDocumentRequirementsQueryHandler(IDocumentRequirementRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetDocumentRequirementsResult> Handle(
        GetDocumentRequirementsQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<DocumentRequirement> requirements;

        if (!string.IsNullOrWhiteSpace(query.PropertyTypeCode))
        {
            if (query.PropertyTypeCode.Equals("APP", StringComparison.OrdinalIgnoreCase))
                requirements = await _repository.GetUniversalRequirementsAsync(cancellationToken);
            else
                requirements = await _repository.GetRequirementsByPropertyTypeAsync(
                    query.PropertyTypeCode, cancellationToken);
        }
        else
        {
            requirements = await _repository.GetAllRequirementsAsync(cancellationToken);
        }

        // Apply purpose filter if provided (client-side for simplicity)
        if (!string.IsNullOrWhiteSpace(query.PurposeCode))
        {
            requirements = requirements
                .Where(r => r.PurposeCode == null || r.PurposeCode == query.PurposeCode)
                .ToList();
        }

        var dtos = requirements.Select(r => new DocumentRequirementDto
        {
            Id = r.Id,
            DocumentTypeId = r.DocumentTypeId,
            DocumentTypeCode = r.DocumentType.Code,
            DocumentTypeName = r.DocumentType.Name,
            DocumentTypeCategory = r.DocumentType.Category,
            PropertyTypeCode = r.PropertyTypeCode,
            PropertyTypeName = GetPropertyTypeName(r.PropertyTypeCode),
            PurposeCode = r.PurposeCode,
            IsRequired = r.IsRequired,
            IsActive = r.IsActive,
            Notes = r.Notes,
            CreatedOn = r.CreatedAt,
            UpdatedOn = r.UpdatedAt
        }).ToList();

        return new GetDocumentRequirementsResult(dtos);
    }

    private static string? GetPropertyTypeName(string? code)
    {
        if (code is null) return "Application Level";
        return PropertyTypeNames.TryGetValue(code, out var name) ? name : code;
    }
}
