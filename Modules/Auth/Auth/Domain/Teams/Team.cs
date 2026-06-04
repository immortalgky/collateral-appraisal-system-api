namespace Auth.Domain.Teams;

/// <summary>
/// Maps to the pre-existing auth.Teams table (DbUp-owned, ExcludeFromMigrations).
/// Only contains the 4 columns that actually exist: Id, Name, Type, IsActive.
/// Does NOT inherit Entity[Guid] — the real table has no audit columns.
/// </summary>
public class Team
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string Type { get; private set; } = "Internal";
    public bool IsActive { get; private set; } = true;

    public List<TeamMember> Members { get; private set; } = [];

    private Team() { }

    public static Team Create(string name, string type = "Internal", bool isActive = true)
    {
        return new Team
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            Type = type,
            IsActive = isActive
        };
    }

    public void Update(string name, string type, bool isActive)
    {
        Name = name;
        Type = type;
        IsActive = isActive;
    }
}
