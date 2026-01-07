namespace Appraisal.Domain.DocumentRequirements;

/// <summary>
/// Master list of document types that can be required for appraisals.
/// Examples: Title Deed, Survey Map, Floor Plan, Vehicle Registration, etc.
/// </summary>
public class DocumentType : Entity<Guid>
{
    private readonly List<DocumentRequirement> _requirements = [];

    public IReadOnlyList<DocumentRequirement> Requirements => _requirements.AsReadOnly();

    /// <summary>
    /// Unique code for the document type (e.g., "TD", "SM", "FP")
    /// </summary>
    public string Code { get; private set; } = null!;

    /// <summary>
    /// Display name (e.g., "Title Deed", "Survey Map")
    /// </summary>
    public string Name { get; private set; } = null!;

    /// <summary>
    /// Optional description of the document type
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Category for grouping in UI (e.g., "Legal", "Technical", "General")
    /// </summary>
    public string? Category { get; private set; }

    /// <summary>
    /// Whether this document type is active
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Sort order for display purposes
    /// </summary>
    public int SortOrder { get; private set; }

    // Private constructor for EF Core
    private DocumentType()
    {
    }

    private DocumentType(string code, string name, string? description, string? category, int sortOrder)
    {
        Id = Guid.NewGuid();
        Code = code;
        Name = name;
        Description = description;
        Category = category;
        SortOrder = sortOrder;
        IsActive = true;
    }

    /// <summary>
    /// Factory method to create a new DocumentType
    /// </summary>
    public static DocumentType Create(
        string code,
        string name,
        string? description = null,
        string? category = null,
        int sortOrder = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new DocumentType(code.ToUpperInvariant(), name, description, category, sortOrder);
    }

    /// <summary>
    /// Update the document type details
    /// </summary>
    public void Update(string name, string? description, string? category, int sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        Description = description;
        Category = category;
        SortOrder = sortOrder;
    }

    /// <summary>
    /// Activate the document type
    /// </summary>
    public void Activate()
    {
        IsActive = true;
    }

    /// <summary>
    /// Deactivate the document type
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }
}
