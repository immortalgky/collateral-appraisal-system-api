using Auth.Domain.Groups;

namespace Auth.Infrastructure.Configurations;

public class GroupMonitoringConfiguration : IEntityTypeConfiguration<GroupMonitoring>
{
    public void Configure(EntityTypeBuilder<GroupMonitoring> builder)
    {
        builder.ToTable("GroupMonitoring", "auth");

        builder.HasKey(gm => new { gm.MonitorGroupId, gm.MonitoredGroupId });

        builder.HasOne(gm => gm.MonitorGroup)
            .WithMany(g => g.MonitoredGroups)
            .HasForeignKey(gm => gm.MonitorGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(gm => gm.MonitoredGroup)
            .WithMany()
            .HasForeignKey(gm => gm.MonitoredGroupId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
