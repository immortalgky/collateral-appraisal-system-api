namespace Appraisal.Domain.DocumentRequirements;

/// <summary>
/// Defines which documents are required for a specific property type and/or purpose.
/// 4 tiers of requirements:
/// - Tier 1 (Universal): PropertyTypeCode=NULL, PurposeCode=NULL — always required
/// - Tier 2 (Purpose-only): PropertyTypeCode=NULL, PurposeCode=NOT NULL — required for a purpose
/// - Tier 3 (PropertyType-only): PropertyTypeCode=NOT NULL, PurposeCode=NULL — base for a property type
/// - Tier 4 (Fully specific): PropertyTypeCode=NOT NULL, PurposeCode=NOT NULL — specific to combination
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
    /// Property type code (e.g., "L", "B", "LB", "U", "VEH", "VES", "MAC").
    /// NULL means this applies regardless of property type.
    /// </summary>
    public string? PropertyTypeCode { get; private set; }

    /// <summary>
    /// Purpose code (e.g., "01", "02", from Parameter module "Objective" group).
    /// NULL means this applies regardless of purpose.
    /// </summary>
    public string? PurposeCode { get; private set; }

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
        string? propertyTypeCode,
        string? purposeCode,
        bool isRequired,
        string? notes)
    {
        Id = Guid.CreateVersion7();
        DocumentTypeId = documentTypeId;
        PropertyTypeCode = propertyTypeCode?.ToUpperInvariant();
        PurposeCode = purposeCode;
        IsRequired = isRequired;
        Notes = notes;
        IsActive = true;
    }

    /// <summary>
    /// Tier 1: Universal requirement (applies to ALL appraisals regardless of property type or purpose)
    /// </summary>
    public static DocumentRequirement CreateApplicationLevel(
        Guid documentTypeId,
        bool isRequired,
        string? notes = null)
    {
        return new DocumentRequirement(documentTypeId, null, null, isRequired, notes);
    }

    /// <summary>
    /// Tier 2: Purpose-only requirement (applies to a specific purpose regardless of property type)
    /// </summary>
    public static DocumentRequirement CreateForPurpose(
        Guid documentTypeId,
        string purposeCode,
        bool isRequired,
        string? notes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purposeCode);

        return new DocumentRequirement(documentTypeId, null, purposeCode, isRequired, notes);
    }

    /// <summary>
    /// Tier 3: PropertyType-only requirement (applies to a specific property type regardless of purpose)
    /// </summary>
    public static DocumentRequirement CreateForPropertyType(
        Guid documentTypeId,
        string propertyTypeCode,
        bool isRequired,
        string? notes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyTypeCode);

        return new DocumentRequirement(documentTypeId, propertyTypeCode, null, isRequired, notes);
    }

    /// <summary>
    /// Tier 4: Fully specific requirement (applies to a specific property type + purpose combination)
    /// </summary>
    public static DocumentRequirement CreateForPropertyTypeAndPurpose(
        Guid documentTypeId,
        string propertyTypeCode,
        string purposeCode,
        bool isRequired,
        string? notes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyTypeCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(purposeCode);

        return new DocumentRequirement(documentTypeId, propertyTypeCode, purposeCode, isRequired, notes);
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
    /// Check if this is an application-level (universal) requirement — both PropertyTypeCode and PurposeCode are null
    /// </summary>
    public bool IsApplicationLevel => PropertyTypeCode is null && PurposeCode is null;
}
