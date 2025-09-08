using Workflow.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Workflow.Data.Configurations;

public class TaskAssignmentConfigurationConfiguration : IEntityTypeConfiguration<TaskAssignmentConfiguration>
{
    public void Configure(EntityTypeBuilder<TaskAssignmentConfiguration> builder)
    {
        builder.ToTable("TaskAssignmentConfigurations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ActivityId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.WorkflowDefinitionId)
            .HasMaxLength(100);

        builder.Property(x => x.PrimaryStrategies)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.RouteBackStrategies)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.AdminPoolId)
            .HasMaxLength(100);

        builder.Property(x => x.EscalateToAdminPool)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.SpecificAssignee)
            .HasMaxLength(100);

        builder.Property(x => x.AssigneeGroup)
            .HasMaxLength(100);

        // NOTE: SupervisorId and ReplacementUserId properties removed - now handled by UserManagement mock data

        builder.Property(x => x.AdditionalConfiguration)
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

        // Indexes for performance
        builder.HasIndex(x => x.ActivityId)
            .HasDatabaseName("IX_TaskAssignmentConfigurations_ActivityId");

        builder.HasIndex(x => new { x.ActivityId, x.WorkflowDefinitionId })
            .HasDatabaseName("IX_TaskAssignmentConfigurations_ActivityId_WorkflowDefinitionId");

        builder.HasIndex(x => x.IsActive)
            .HasDatabaseName("IX_TaskAssignmentConfigurations_IsActive");
    }
}