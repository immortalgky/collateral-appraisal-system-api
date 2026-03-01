namespace Appraisal.Domain.DocumentRequirements;

/// <summary>
/// Repository interface for DocumentType and DocumentRequirement entities
/// </summary>
public interface IDocumentRequirementRepository
{
    #region DocumentType Operations

    /// <summary>
    /// Get all active document types
    /// </summary>
    Task<IReadOnlyList<DocumentType>> GetAllDocumentTypesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a document type by ID
    /// </summary>
    Task<DocumentType?> GetDocumentTypeByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a document type by code
    /// </summary>
    Task<DocumentType?> GetDocumentTypeByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Add a new document type
    /// </summary>
    void AddDocumentType(DocumentType documentType);

    /// <summary>
    /// Update an existing document type
    /// </summary>
    void UpdateDocumentType(DocumentType documentType);

    #endregion

    #region DocumentRequirement Operations

    /// <summary>
    /// Get all document requirements (admin view)
    /// </summary>
    Task<IReadOnlyList<DocumentRequirement>> GetAllRequirementsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a requirement by ID
    /// </summary>
    Task<DocumentRequirement?> GetRequirementByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get universal requirements (PropertyTypeCode IS NULL AND PurposeCode IS NULL) — Tier 1
    /// </summary>
    Task<IReadOnlyList<DocumentRequirement>> GetUniversalRequirementsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get purpose-only requirements (PropertyTypeCode IS NULL AND PurposeCode = purposeCode) — Tier 2
    /// </summary>
    Task<IReadOnlyList<DocumentRequirement>> GetPurposeOnlyRequirementsAsync(
        string purposeCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get requirements for a specific property type (PropertyTypeCode = code AND PurposeCode IS NULL) — Tier 3
    /// </summary>
    Task<IReadOnlyList<DocumentRequirement>> GetRequirementsByPropertyTypeAsync(
        string propertyTypeCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get requirements for multiple property types with optional purpose filtering.
    /// Returns Tier 3 (PurposeCode IS NULL) and optionally Tier 4 (PurposeCode = purposeCode).
    /// </summary>
    Task<IReadOnlyList<DocumentRequirement>> GetRequirementsByPropertyTypesAsync(
        IEnumerable<string> propertyTypeCodes,
        string? purposeCode = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a requirement already exists for the given document type, property type, and purpose
    /// </summary>
    Task<bool> RequirementExistsAsync(
        Guid documentTypeId,
        string? propertyTypeCode,
        string? purposeCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Add a new requirement
    /// </summary>
    void AddRequirement(DocumentRequirement requirement);

    /// <summary>
    /// Update an existing requirement
    /// </summary>
    void UpdateRequirement(DocumentRequirement requirement);

    /// <summary>
    /// Delete a requirement
    /// </summary>
    void DeleteRequirement(DocumentRequirement requirement);

    #endregion
}
