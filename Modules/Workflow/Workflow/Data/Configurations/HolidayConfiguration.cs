using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workflow.Sla.Models;

namespace Workflow.Data.Configurations;

public class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> builder)
    {
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Date)
            .IsRequired();

        builder.Property(h => h.Description)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(h => h.Year)
            .IsRequired();

        builder.HasIndex(h => h.Year);
        builder.HasIndex(h => h.Date).IsUnique();
    }
}
