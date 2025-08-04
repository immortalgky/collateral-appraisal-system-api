using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Assignment.Data.Configurations;

public class RoundRobinQueueConfiguration : IEntityTypeConfiguration<RoundRobinQueue>
{
    public void Configure(EntityTypeBuilder<RoundRobinQueue> builder)
    {
        builder.HasKey(x => new { x.ActivityName, x.GroupsHash, x.UserId });

        builder.Property(x => x.ActivityName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.GroupsHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.GroupsList)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.UserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(x => x.AssignmentCount)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.LastAssignedAt)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.HasIndex(x => new { x.ActivityName, x.GroupsHash, x.IsActive, x.AssignmentCount })
            .HasDatabaseName("IX_UserAssignmentCounters_Selection");
    }
}