using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workflow.Sla.Models;

namespace Workflow.Data.Configurations;

public class WorkflowSlaConfigurationConfiguration : IEntityTypeConfiguration<WorkflowSlaConfiguration>
{
    public void Configure(EntityTypeBuilder<WorkflowSlaConfiguration> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.LoanType)
            .HasMaxLength(50)
            .IsRequired(false);

        builder.Property(s => s.Priority)
            .IsRequired();

        builder.HasIndex(s => new { s.WorkflowDefinitionId, s.LoanType })
            .IsUnique()
            .HasFilter(null);
    }
}
