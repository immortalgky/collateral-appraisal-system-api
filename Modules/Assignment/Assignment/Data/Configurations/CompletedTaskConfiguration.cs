using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Assignment.Data.Configurations;

public class CompletedTaskConfiguration : IEntityTypeConfiguration<CompletedTask>
{
    public void Configure(EntityTypeBuilder<CompletedTask> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.TaskName)
            .HasMaxLength(100);

        builder.Property(p => p.AssignedTo)
            .HasMaxLength(10);

        builder.Property(p => p.AssignedType)
            .HasMaxLength(10);

        builder.Property(p => p.ActionTaken)
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