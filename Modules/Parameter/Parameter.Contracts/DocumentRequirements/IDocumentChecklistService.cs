namespace Parameter.Contracts.DocumentRequirements;

public interface IDocumentChecklistService
{
    Task<IReadOnlyList<DocumentChecklistItemDto>> GetApplicationRequirementsAsync(
        string? purposeCode, CancellationToken ct);

    Task<IReadOnlyList<CollateralTypeDocumentGroupDto>> GetCollateralTypeRequirementsAsync(
        IEnumerable<string> collateralTypeCodes, string? purposeCode, CancellationToken ct);
}
