using Parameter.Contracts.DocumentRequirements;

namespace Parameter.DocumentRequirements.Services;

public class DocumentChecklistService : IDocumentChecklistService
{
    private readonly IDocumentRequirementRepository _repository;

    public DocumentChecklistService(IDocumentRequirementRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<DocumentChecklistItemDto>> GetApplicationRequirementsAsync(
        string? purposeCode, CancellationToken ct)
    {
        // Tier 1: Universal
        var universalRequirements = await _repository.GetUniversalRequirementsAsync(ct);
        var result = universalRequirements.Select(MapToDto).ToList();

        // Tier 2: Purpose-only (merge/override)
        if (!string.IsNullOrWhiteSpace(purposeCode))
        {
            var purposeRequirements = await _repository.GetPurposeOnlyRequirementsAsync(purposeCode, ct);
            var existingCodes = new HashSet<string>(result.Select(d => d.Code));

            foreach (var req in purposeRequirements)
            {
                var code = req.DocumentType.Code;
                if (existingCodes.Contains(code))
                {
                    var index = result.FindIndex(d => d.Code == code);
                    result[index] = MapToDto(req);
                }
                else
                {
                    result.Add(MapToDto(req));
                    existingCodes.Add(code);
                }
            }
        }

        return result;
    }

    public async Task<IReadOnlyList<CollateralTypeDocumentGroupDto>> GetCollateralTypeRequirementsAsync(
        IEnumerable<string> collateralTypeCodes, string? purposeCode, CancellationToken ct)
    {
        var codes = collateralTypeCodes.Select(c => c.ToUpperInvariant()).Distinct().ToList();
        if (codes.Count == 0) return [];

        var requirements = await _repository.GetRequirementsByPropertyTypesAsync(codes, purposeCode, ct);

        return requirements
            .GroupBy(r => r.PropertyTypeCode!)
            .OrderBy(g => codes.IndexOf(g.Key))
            .Select(group =>
            {
                var documents = group
                    .GroupBy(r => r.DocumentType.Code)
                    .Select(g => g.OrderByDescending(r => r.PurposeCode is not null ? 1 : 0).First())
                    .Select(MapToDto)
                    .ToList();

                return new CollateralTypeDocumentGroupDto(group.Key, documents);
            })
            .ToList();
    }

    private static DocumentChecklistItemDto MapToDto(Models.DocumentRequirement req) => new()
    {
        Code = req.DocumentType.Code,
        Name = req.DocumentType.Name,
        Category = req.DocumentType.Category,
        IsRequired = req.IsRequired,
        Notes = req.Notes
    };
}
