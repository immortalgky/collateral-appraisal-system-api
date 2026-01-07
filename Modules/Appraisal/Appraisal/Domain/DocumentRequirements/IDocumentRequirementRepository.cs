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
    /// Get application-level requirements (CollateralTypeCode IS NULL)
    /// </summary>
    Task<IReadOnlyList<DocumentRequirement>> GetApplicationLevelRequirementsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get requirements for a specific collateral type
    /// </summary>
    Task<IReadOnlyList<DocumentRequirement>> GetRequirementsByCollateralTypeAsync(
        string collateralTypeCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get requirements for multiple collateral types (for multi-collateral appraisals)
    /// </summary>
    Task<IReadOnlyList<DocumentRequirement>> GetRequirementsByCollateralTypesAsync(
        IEnumerable<string> collateralTypeCodes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a requirement already exists for the given document type and collateral type
    /// </summary>
    Task<bool> RequirementExistsAsync(
        Guid documentTypeId,
        string? collateralTypeCode,
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
