using Auth.Domain.Teams;

namespace Auth.Infrastructure.Configurations;

/// <summary>
/// Maps Team to the pre-existing auth.Teams table (DbUp-owned).
/// ExcludeFromMigrations = EF can SELECT/INSERT/UPDATE but will NOT scaffold
/// this table in any migration.
/// Only the 4 columns that actually exist in the DB are mapped.
/// </summary>
public class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("Teams", "auth", t => t.ExcludeFromMigrations());

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Type).IsRequired().HasMaxLength(50).HasDefaultValue("Internal");
        builder.Property(t => t.IsActive).IsRequired().HasDefaultValue(true);

        // Ignore all Entity<Guid> audit columns — they do not exist in the real table
        builder.Ignore("CreatedAt");
        builder.Ignore("CreatedBy");
        builder.Ignore("CreatedWorkstation");
        builder.Ignore("UpdatedAt");
        builder.Ignore("UpdatedBy");
        builder.Ignore("UpdatedWorkstation");

        builder.HasMany(t => t.Members)
            .WithOne(m => m.Team)
            .HasForeignKey(m => m.TeamId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
