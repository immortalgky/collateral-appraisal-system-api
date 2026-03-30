using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workflow.Sla.Models;

namespace Workflow.Data.Configurations;

public class SlaConfigurationConfiguration : IEntityTypeConfiguration<SlaConfiguration>
{
    public void Configure(EntityTypeBuilder<SlaConfiguration> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.ActivityId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.LoanType)
            .HasMaxLength(50)
            .IsRequired(false);

        builder.Property(s => s.Priority)
            .IsRequired();

        builder.HasIndex(s => new { s.ActivityId, s.WorkflowDefinitionId, s.CompanyId, s.LoanType })
            .IsUnique()
            .HasFilter(null);
    }
}
