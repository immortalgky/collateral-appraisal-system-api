using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workflow.Domain.Committees;

namespace Workflow.Data.Configurations;

public class CommitteeThresholdConfiguration : IEntityTypeConfiguration<CommitteeThreshold>
{
    public void Configure(EntityTypeBuilder<CommitteeThreshold> builder)
    {
        builder.ToTable("CommitteeThresholds");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.MinValue).HasPrecision(18, 2);
        builder.Property(t => t.MaxValue).HasPrecision(18, 2);

        builder.HasIndex(t => t.CommitteeId);
    }
}
