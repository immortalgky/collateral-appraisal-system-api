using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Workflow.Data.Configurations;

public class PendingTaskConfiguration : IEntityTypeConfiguration<PendingTask>
{
    public void Configure(EntityTypeBuilder<PendingTask> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.TaskName)
            .HasMaxLength(100);

        builder.Property(p => p.TaskDescription)
            .HasMaxLength(200)
            .IsRequired(false);

        builder.Property(p => p.AssignedTo)
            .HasMaxLength(255);

        builder.Property(p => p.AssignedType)
            .HasMaxLength(10);

        builder.Property(p => p.WorkingBy)
            .HasMaxLength(255)
            .IsRequired(false);

        builder.Property(p => p.WorkflowInstanceId);

        builder.Property(p => p.ActivityId)
            .HasMaxLength(100);

        builder.OwnsOne(p => p.TaskStatus,
            taskStatus =>
            {
                taskStatus.Property(p => p.Code)
                    .HasColumnName("TaskStatus")
                    .HasMaxLength(100);
            });

        builder.Property(p => p.DueAt)
            .IsRequired(false);

        builder.Property(p => p.SlaStatus)
            .HasMaxLength(20)
            .IsRequired(false);

        builder.Property(p => p.SlaBreachedAt)
            .IsRequired(false);

        builder.Property(p => p.Movement)
            .HasMaxLength(16)
            .IsRequired()
            .HasDefaultValue("Forward");
    }
}