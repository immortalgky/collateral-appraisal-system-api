namespace Parameter.ConstructionWork.Models;

/// <summary>
/// Lookup entity for construction work items within a group (seeded via SQL script).
/// e.g. Pillar, Floor, Stair under Building Structure group.
/// </summary>
public class ConstructionWorkItem : Entity<Guid>
{
    public Guid ConstructionWorkGroupId { get; private set; }
    public string Code { get; private set; } = null!;
    public string NameTh { get; private set; } = null!;
    public string NameEn { get; private set; } = null!;
    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; }

    private ConstructionWorkItem()
    {
    }
}
