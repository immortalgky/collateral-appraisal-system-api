namespace Appraisal.Domain.DocumentRequirements;

/// <summary>
/// Defines which documents are required for a specific collateral type.
/// When CollateralTypeCode is NULL, the requirement applies to ALL appraisals (application-level).
/// </summary>
public class DocumentRequirement : Entity<Guid>
{
    /// <summary>
    /// Reference to the document type
    /// </summary>
    public Guid DocumentTypeId { get; private set; }

    /// <summary>
    /// Navigation property to DocumentType
    /// </summary>
    public DocumentType DocumentType { get; private set; } = null!;

    /// <summary>
    /// Collateral type code (e.g., "L", "B", "LB", "U", "VEH", "VES", "MAC").
    /// NULL means this is an application-level requirement (required for ALL appraisals).
    /// </summary>
    public string? CollateralTypeCode { get; private set; }

    /// <summary>
    /// Whether this document is required (true) or optional (false)
    /// </summary>
    public bool IsRequired { get; private set; }

    /// <summary>
    /// Whether this requirement is active
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Optional notes/instructions for this requirement
    /// </summary>
    public string? Notes { get; private set; }

    // Private constructor for EF Core
    private DocumentRequirement()
    {
    }

    private DocumentRequirement(
        Guid documentTypeId,
        string? collateralTypeCode,
        bool isRequired,
        string? notes)
    {
        Id = Guid.CreateVersion7();
        DocumentTypeId = documentTypeId;
        CollateralTypeCode = collateralTypeCode?.ToUpperInvariant();
        IsRequired = isRequired;
        Notes = notes;
        IsActive = true;
    }

    /// <summary>
    /// Factory method to create an application-level requirement (applies to ALL appraisals)
    /// </summary>
    public static DocumentRequirement CreateApplicationLevel(
        Guid documentTypeId,
        bool isRequired,
        string? notes = null)
    {
        return new DocumentRequirement(documentTypeId, null, isRequired, notes);
    }

    /// <summary>
    /// Factory method to create a collateral-specific requirement
    /// </summary>
    public static DocumentRequirement CreateForCollateral(
        Guid documentTypeId,
        string collateralTypeCode,
        bool isRequired,
        string? notes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collateralTypeCode);

        return new DocumentRequirement(documentTypeId, collateralTypeCode, isRequired, notes);
    }

    /// <summary>
    /// Update the requirement details
    /// </summary>
    public void Update(bool isRequired, string? notes)
    {
        IsRequired = isRequired;
        Notes = notes;
    }

    /// <summary>
    /// Activate the requirement
    /// </summary>
    public void Activate()
    {
        IsActive = true;
    }

    /// <summary>
    /// Deactivate the requirement
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }

    /// <summary>
    /// Check if this is an application-level requirement
    /// </summary>
    public bool IsApplicationLevel => CollateralTypeCode is null;
}
