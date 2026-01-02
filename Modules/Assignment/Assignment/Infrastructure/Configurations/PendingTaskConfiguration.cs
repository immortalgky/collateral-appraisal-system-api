using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Assignment.Data.Configurations;

public class PendingTaskConfiguration : IEntityTypeConfiguration<PendingTask>
{
    public void Configure(EntityTypeBuilder<PendingTask> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.TaskName)
            .HasConversion<string>()
            .HasMaxLength(100);

        builder.Property(p => p.AssignedTo)
            .HasMaxLength(10);

        builder.Property(p => p.AssignedType)
            .HasMaxLength(10);

        builder.OwnsOne(p => p.TaskStatus,
            taskStatus =>
            {
                taskStatus.Property(p => p.Code)
                    .HasColumnName("TaskStatus")
                    .HasMaxLength(100);
            });
    }
}