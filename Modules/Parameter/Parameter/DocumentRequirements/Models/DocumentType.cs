namespace Parameter.DocumentRequirements.Models;

public class DocumentType : Entity<Guid>
{
    private readonly List<DocumentRequirement> _requirements = [];

    public IReadOnlyList<DocumentRequirement> Requirements => _requirements.AsReadOnly();

    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? NameTh { get; private set; }
    public string? Description { get; private set; }
    public string? Category { get; private set; }
    public bool IsActive { get; private set; } = true;
    public int SortOrder { get; private set; }

    /// <summary>
    /// When a request references a previous appraisal: true = files of this type are carried over
    /// and used by default, false = offered but not linked until the maker opts in.
    /// </summary>
    public bool CarryForwardByDefault { get; private set; } = true;

    private static readonly string[] CarryForwardLockedCodes = ["D036", "D042", "D043"];

    /// <summary>
    /// Code reads these by code and carries them unconditionally (the summary report D036 and the
    /// D042/D043 originals it is re-typed from), so "don't use by default" is not a valid setting.
    /// </summary>
    public bool IsCarryForwardLocked =>
        CarryForwardLockedCodes.Contains(Code.Trim(), StringComparer.OrdinalIgnoreCase);

    private DocumentType()
    {
    }

    private DocumentType(string code, string name, string? nameTh, string? description, string? category, int sortOrder, bool carryForwardByDefault)
    {
        Id = Guid.CreateVersion7();
        Code = code;
        Name = name;
        NameTh = nameTh;
        Description = description;
        Category = category;
        SortOrder = sortOrder;
        CarryForwardByDefault = carryForwardByDefault;
        IsActive = true;
    }

    public static DocumentType Create(
        string code,
        string name,
        string? description = null,
        string? category = null,
        int sortOrder = 0,
        string? nameTh = null,
        bool carryForwardByDefault = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new DocumentType(code.ToUpperInvariant(), name, nameTh, description, category, sortOrder, carryForwardByDefault);
    }

    public void Update(string name, string? description, string? category, int sortOrder, string? nameTh = null,
        bool? carryForwardByDefault = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        NameTh = nameTh;
        Description = description;
        Category = category;
        SortOrder = sortOrder;
        CarryForwardByDefault = carryForwardByDefault ?? CarryForwardByDefault;
    }

    public void SetSortOrder(int sortOrder)
    {
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
