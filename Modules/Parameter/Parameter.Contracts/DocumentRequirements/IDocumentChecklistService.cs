namespace Parameter.Contracts.DocumentRequirements;

public interface IDocumentChecklistService
{
    Task<IReadOnlyList<DocumentChecklistItemDto>> GetApplicationRequirementsAsync(
        string? purposeCode, CancellationToken ct);

    Task<IReadOnlyList<CollateralTypeDocumentGroupDto>> GetCollateralTypeRequirementsAsync(
        IEnumerable<string> collateralTypeCodes, string? purposeCode, CancellationToken ct);

    /// <summary>
    /// Returns a map of every active DocumentType code → Name.
    /// Codes are canonical-uppercase; callers should uppercase before lookup.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> GetAllDocumentTypeNamesAsync(CancellationToken ct);
}
