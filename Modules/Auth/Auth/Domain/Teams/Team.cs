namespace Auth.Domain.Teams;

/// <summary>
/// Maps to the pre-existing auth.Teams table (DbUp-owned, ExcludeFromMigrations).
/// Columns: Id, Name, Scope, Description.
/// Does NOT inherit Entity[Guid] — the real table has no audit columns.
/// </summary>
public class Team
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string Scope { get; private set; } = "Bank";
    public string? Description { get; private set; }

    public List<TeamMember> Members { get; private set; } = [];

    private Team() { }

    public static Team Create(string name, string scope = "Bank", string? description = null)
    {
        return new Team
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            Scope = scope,
            Description = description
        };
    }

    public void Update(string name, string scope, string? description = null)
    {
        Name = name;
        Scope = scope;
        Description = description;
    }
}
