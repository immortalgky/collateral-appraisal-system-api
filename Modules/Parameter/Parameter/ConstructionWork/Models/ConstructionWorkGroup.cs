namespace Parameter.ConstructionWork.Models;

/// <summary>
/// Lookup entity for construction work groups (seeded via SQL script).
/// e.g. Building Structure, Architecture, Building Management System.
/// </summary>
public class ConstructionWorkGroup : Entity<Guid>
{
    public string Code { get; private set; } = null!;
    public string NameTh { get; private set; } = null!;
    public string NameEn { get; private set; } = null!;
    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; }

    // Navigation
    private readonly List<ConstructionWorkItem> _workItems = [];
    public IReadOnlyList<ConstructionWorkItem> WorkItems => _workItems.AsReadOnly();

    private ConstructionWorkGroup()
    {
    }
}
