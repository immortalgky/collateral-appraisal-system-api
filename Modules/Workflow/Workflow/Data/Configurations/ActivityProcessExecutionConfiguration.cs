using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workflow.Data.Entities;

namespace Workflow.Data.Configurations;

public class ActivityProcessExecutionConfiguration : IEntityTypeConfiguration<ActivityProcessExecution>
{
    public void Configure(EntityTypeBuilder<ActivityProcessExecution> builder)
    {
        builder.ToTable("ActivityProcessExecutions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(x => x.WorkflowInstanceId)
            .IsRequired();

        builder.Property(x => x.WorkflowActivityExecutionId)
            .IsRequired();

        builder.Property(x => x.ConfigurationId);

        builder.Property(x => x.ConfigurationVersion)
            .IsRequired();

        builder.Property(x => x.StepName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Kind)
            .IsRequired()
            .HasColumnType("tinyint");

        builder.Property(x => x.SortOrder)
            .IsRequired();

        builder.Property(x => x.RunIfExpressionSnapshot)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.ParametersJsonSnapshot)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.Outcome)
            .IsRequired()
            .HasColumnType("tinyint");

        builder.Property(x => x.SkipReason)
            .HasColumnType("tinyint");

        builder.Property(x => x.DurationMs)
            .IsRequired();

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(1000);

        builder.Property(x => x.CreatedOn)
            .IsRequired();

        builder.HasIndex(x => x.WorkflowActivityExecutionId)
            .HasDatabaseName("IX_ActivityProcessExecutions_WorkflowActivityExecutionId");
    }
}
