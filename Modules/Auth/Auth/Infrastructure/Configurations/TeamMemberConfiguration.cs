using Auth.Domain.Teams;

namespace Auth.Infrastructure.Configurations;

/// <summary>
/// Maps TeamMember to the pre-existing auth.TeamMembers table (DbUp-owned).
/// ExcludeFromMigrations = EF maps but does NOT scaffold this table.
/// </summary>
public class TeamMemberConfiguration : IEntityTypeConfiguration<TeamMember>
{
    public void Configure(EntityTypeBuilder<TeamMember> builder)
    {
        builder.ToTable("TeamMembers", "auth", t => t.ExcludeFromMigrations());

        builder.HasKey(m => new { m.TeamId, m.UserId });

        builder.HasOne(m => m.Team)
            .WithMany(t => t.Members)
            .HasForeignKey(m => m.TeamId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
