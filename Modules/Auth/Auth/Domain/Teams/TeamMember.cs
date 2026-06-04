namespace Auth.Domain.Teams;

/// <summary>
/// Maps to the pre-existing auth.TeamMembers table (DbUp-owned, ExcludeFromMigrations).
/// </summary>
public class TeamMember
{
    public Guid TeamId { get; set; }
    public Team Team { get; set; } = default!;
    public Guid UserId { get; set; }
}
