using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workflow.Sla.Models;

namespace Workflow.Data.Configurations;

public class SlaBreachLogConfiguration : IEntityTypeConfiguration<SlaBreachLog>
{
    public void Configure(EntityTypeBuilder<SlaBreachLog> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.TaskName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.AssignedTo)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(s => s.SlaStatus)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.NotifiedAt)
            .IsRequired(false);

        builder.HasIndex(s => new { s.PendingTaskId, s.SlaStatus });
    }
}
