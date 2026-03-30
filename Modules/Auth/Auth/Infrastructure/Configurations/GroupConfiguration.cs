using Auth.Domain.Groups;

namespace Auth.Infrastructure.Configurations;

public class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable("Groups", "auth");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Name).IsRequired().HasMaxLength(200);
        builder.Property(g => g.Description).IsRequired().HasMaxLength(500);
        builder.Property(g => g.Scope).IsRequired().HasMaxLength(50);
        builder.Property(g => g.CompanyId);

        builder.HasMany(g => g.Users)
            .WithOne(gu => gu.Group)
            .HasForeignKey(gu => gu.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(g => g.MonitoredGroups)
            .WithOne(gm => gm.MonitorGroup)
            .HasForeignKey(gm => gm.MonitorGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(g => new { g.Name, g.Scope })
            .IsUnique()
            .HasFilter("IsDeleted = 0");

        builder.HasQueryFilter(g => !g.IsDeleted);
    }
}
