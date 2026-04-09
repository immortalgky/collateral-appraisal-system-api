using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Workflow.Data.Configurations;

public class CompletedTaskConfiguration : IEntityTypeConfiguration<CompletedTask>
{
    public void Configure(EntityTypeBuilder<CompletedTask> builder)
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

        builder.Property(p => p.ActionTaken)
            .HasMaxLength(10);

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

        builder.Property(p => p.Remark)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(p => p.Movement)
            .HasMaxLength(16)
            .IsRequired()
            .HasDefaultValue("Forward");
    }
}