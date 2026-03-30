using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workflow.Sla.Models;

namespace Workflow.Data.Configurations;

public class BusinessHoursConfigConfiguration : IEntityTypeConfiguration<BusinessHoursConfig>
{
    public void Configure(EntityTypeBuilder<BusinessHoursConfig> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.TimeZone)
            .HasMaxLength(100)
            .IsRequired();
    }
}
