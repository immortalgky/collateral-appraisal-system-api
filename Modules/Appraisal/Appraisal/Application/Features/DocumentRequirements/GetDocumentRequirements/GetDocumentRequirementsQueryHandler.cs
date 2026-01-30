namespace Appraisal.Application.Features.DocumentRequirements.GetDocumentRequirements;

/// <summary>
/// Handler for GetDocumentRequirementsQuery
/// </summary>
public class GetDocumentRequirementsQueryHandler : IQueryHandler<GetDocumentRequirementsQuery, GetDocumentRequirementsResult>
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

    public GetDocumentRequirementsQueryHandler(IDocumentRequirementRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetDocumentRequirementsResult> Handle(
        GetDocumentRequirementsQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<DocumentRequirement> requirements;

        if (!string.IsNullOrWhiteSpace(query.CollateralTypeCode))
        {
            // Filter by collateral type
            if (query.CollateralTypeCode.Equals("APP", StringComparison.OrdinalIgnoreCase))
            {
                // Application-level requirements
                requirements = await _repository.GetApplicationLevelRequirementsAsync(cancellationToken);
            }
            else
            {
                requirements = await _repository.GetRequirementsByCollateralTypeAsync(
                    query.CollateralTypeCode, cancellationToken);
            }
        }
        else
        {
            // Get all requirements
            requirements = await _repository.GetAllRequirementsAsync(cancellationToken);
        }

        var dtos = requirements.Select(r => new DocumentRequirementDto
        {
            Id = r.Id,
            DocumentTypeId = r.DocumentTypeId,
            DocumentTypeCode = r.DocumentType.Code,
            DocumentTypeName = r.DocumentType.Name,
            DocumentTypeCategory = r.DocumentType.Category,
            CollateralTypeCode = r.CollateralTypeCode,
            CollateralTypeName = GetCollateralTypeName(r.CollateralTypeCode),
            IsRequired = r.IsRequired,
            IsActive = r.IsActive,
            Notes = r.Notes,
            CreatedOn = r.CreatedOn,
            UpdatedOn = r.UpdatedOn
        }).ToList();

        return new GetDocumentRequirementsResult(dtos);
    }

    private static string? GetCollateralTypeName(string? code)
    {
        if (code is null) return "Application Level";
        return CollateralTypeNames.TryGetValue(code, out var name) ? name : code;
    }
}
