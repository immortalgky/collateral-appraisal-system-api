using Workflow.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Workflow.Data.Configurations;

public class ActivityProcessConfigurationConfiguration : IEntityTypeConfiguration<ActivityProcessConfiguration>
{
    public void Configure(EntityTypeBuilder<ActivityProcessConfiguration> builder)
    {
        builder.ToTable("ActivityProcessConfigurations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ActivityName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.StepName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.ProcessorName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.SortOrder)
            .IsRequired();

        builder.Property(x => x.Parameters)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.UpdatedBy)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(x => x.ActivityName)
            .HasDatabaseName("IX_ActivityProcessConfigurations_ActivityName");

        builder.HasIndex(x => new { x.ActivityName, x.IsActive, x.SortOrder })
            .HasDatabaseName("IX_ActivityProcessConfigurations_Activity_Active_Sort");
    }
}
