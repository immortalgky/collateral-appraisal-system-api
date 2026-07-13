using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Workflow.Data.Configurations;

public class CompletedTaskConfiguration : IEntityTypeConfiguration<CompletedTask>
{
    public void Configure(EntityTypeBuilder<CompletedTask> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.ActivityId)
            .HasMaxLength(100)
            .IsRequired(false);

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
            .HasMaxLength(255);

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
            .HasMaxLength(4000)
            .IsRequired(false);

        builder.Property(p => p.Movement)
            .HasMaxLength(16)
            .IsRequired()
            .HasDefaultValue("F");

        builder.Property(p => p.ReasonCode)
            .HasMaxLength(20)
            .IsRequired(false);

        builder.Property(p => p.AssigneeCompanyId)
            .IsRequired(false);

        // Covering index for CorrelationId lookups against this append-only archival table.
        // Serves the non-admin access-control EXISTS in GetQuotationsQueryHandler
        // (EXISTS ... FROM workflow.CompletedTasks ct WHERE ct.CorrelationId = q.Id AND <user/company gate>)
        // and routeback dedup reads. Without it those queries clustered-index-scan a table that
        // grows without bound. INCLUDE carries the gate columns so the seek is fully covered.
        builder.HasIndex(p => p.CorrelationId)
            .HasDatabaseName("IX_CompletedTasks_CorrelationId")
            .IncludeProperties(p => new { p.AssignedType, p.AssignedTo, p.AssigneeCompanyId });
    }
}