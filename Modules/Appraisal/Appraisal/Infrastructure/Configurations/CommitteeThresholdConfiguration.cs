namespace Appraisal.Infrastructure.Configurations;

public class CommitteeThresholdConfiguration : IEntityTypeConfiguration<CommitteeThreshold>
{
    public void Configure(EntityTypeBuilder<CommitteeThreshold> builder)
    {
        builder.ToTable("CommitteeThresholds");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(t => t.CommitteeId).IsRequired();
        builder.Property(t => t.MinValue).IsRequired().HasPrecision(18, 2);
        builder.Property(t => t.MaxValue).HasPrecision(18, 2);
        builder.Property(t => t.Priority).IsRequired();

        builder.HasIndex(t => t.CommitteeId);
    }
}
