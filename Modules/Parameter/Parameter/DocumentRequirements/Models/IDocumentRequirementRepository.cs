namespace Parameter.DocumentRequirements.Models;

public interface IDocumentRequirementRepository
{
    #region DocumentType Operations

    Task<IReadOnlyList<DocumentType>> GetAllDocumentTypesAsync(CancellationToken cancellationToken = default);
    Task<DocumentType?> GetDocumentTypeByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DocumentType?> GetDocumentTypeByCodeAsync(string code, CancellationToken cancellationToken = default);
    void AddDocumentType(DocumentType documentType);
    void UpdateDocumentType(DocumentType documentType);

    #endregion

    #region DocumentRequirement Operations

    Task<IReadOnlyList<DocumentRequirement>> GetAllRequirementsAsync(CancellationToken cancellationToken = default);
    Task<DocumentRequirement?> GetRequirementByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentRequirement>> GetUniversalRequirementsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentRequirement>> GetPurposeOnlyRequirementsAsync(
        string purposeCode,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentRequirement>> GetRequirementsByPropertyTypeAsync(
        string propertyTypeCode,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentRequirement>> GetRequirementsByPropertyTypesAsync(
        IEnumerable<string> propertyTypeCodes,
        string? purposeCode = null,
        CancellationToken cancellationToken = default);

    Task<bool> RequirementExistsAsync(
        Guid documentTypeId,
        string? propertyTypeCode,
        string? purposeCode,
        CancellationToken cancellationToken = default);

    void AddRequirement(DocumentRequirement requirement);
    void UpdateRequirement(DocumentRequirement requirement);
    void DeleteRequirement(DocumentRequirement requirement);

    #endregion
}
