using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workflow.Sla.Models;

namespace Workflow.Data.Configurations;

public class SlaPolicyConfiguration : IEntityTypeConfiguration<SlaPolicy>
{
    public void Configure(EntityTypeBuilder<SlaPolicy> builder)
    {
        builder.ToTable("SlaPolicies");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.ActivityId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.LoanType)
            .HasMaxLength(50)
            .IsRequired(false);

        builder.Property(s => s.Priority)
            .IsRequired();

        builder.Property(s => s.Scope)
            .IsRequired()
            .HasDefaultValue(SlaPolicyScope.Activity);

        builder.Property(s => s.StartActivityKey)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(s => s.EndActivityKey)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(s => s.MiddleActivityKeys)
            .HasColumnType("nvarchar(max)")
            .IsRequired(false);

        // M2: Filtered unique indexes per scope so FirstOrDefaultAsync is deterministic.
        builder.HasIndex(
                s => new { s.ActivityId, s.WorkflowDefinitionId, s.CompanyId, s.LoanType, s.Priority })
            .IsUnique()
            .HasFilter("[Scope] = 1")
            .HasDatabaseName("IX_SlaPolicies_Activity");

        builder.HasIndex(
                s => new { s.StartActivityKey, s.WorkflowDefinitionId, s.CompanyId, s.LoanType, s.Priority })
            .IsUnique()
            .HasFilter("[Scope] = 2")
            .HasDatabaseName("IX_SlaPolicies_Stage_Start");

        // Workflow-scope uniqueness: per (WorkflowDefinitionId, LoanType) to allow one row
        // per loan type per workflow, matching CalculateWorkflowDueAtAsync's LoanType filter.
        builder.HasIndex(s => new { s.WorkflowDefinitionId, s.LoanType })
            .IsUnique()
            .HasFilter("[Scope] = 3")
            .HasDatabaseName("IX_SlaPolicies_Workflow");
    }
}
