namespace Parameter.DocumentRequirements.Models;

public class DocumentRequirement : Entity<Guid>
{
    public Guid DocumentTypeId { get; private set; }
    public DocumentType DocumentType { get; private set; } = null!;
    public string? PropertyTypeCode { get; private set; }
    public string? PurposeCode { get; private set; }
    public bool IsRequired { get; private set; }
    public bool IsActive { get; private set; } = true;
    public string? Notes { get; private set; }

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

    public static DocumentRequirement CreateApplicationLevel(
        Guid documentTypeId,
        bool isRequired,
        string? notes = null)
    {
        return new DocumentRequirement(documentTypeId, null, null, isRequired, notes);
    }

    public static DocumentRequirement CreateForPurpose(
        Guid documentTypeId,
        string purposeCode,
        bool isRequired,
        string? notes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purposeCode);
        return new DocumentRequirement(documentTypeId, null, purposeCode, isRequired, notes);
    }

    public static DocumentRequirement CreateForPropertyType(
        Guid documentTypeId,
        string propertyTypeCode,
        bool isRequired,
        string? notes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyTypeCode);
        return new DocumentRequirement(documentTypeId, propertyTypeCode, null, isRequired, notes);
    }

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

    public void Update(bool isRequired, string? notes)
    {
        IsRequired = isRequired;
        Notes = notes;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public bool IsApplicationLevel => PropertyTypeCode is null && PurposeCode is null;
}
