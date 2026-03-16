namespace Parameter.DocumentRequirements.Models;

public class DocumentType : Entity<Guid>
{
    private readonly List<DocumentRequirement> _requirements = [];

    public IReadOnlyList<DocumentRequirement> Requirements => _requirements.AsReadOnly();

    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public string? Category { get; private set; }
    public bool IsActive { get; private set; } = true;
    public int SortOrder { get; private set; }

    private DocumentType()
    {
    }

    private DocumentType(string code, string name, string? description, string? category, int sortOrder)
    {
        Id = Guid.CreateVersion7();
        Code = code;
        Name = name;
        Description = description;
        Category = category;
        SortOrder = sortOrder;
        IsActive = true;
    }

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

    public void Update(string name, string? description, string? category, int sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        Description = description;
        Category = category;
        SortOrder = sortOrder;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
