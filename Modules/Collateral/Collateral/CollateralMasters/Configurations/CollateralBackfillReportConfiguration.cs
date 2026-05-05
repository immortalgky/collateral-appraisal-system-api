using Collateral.CollateralMasters.Models;

namespace Collateral.CollateralMasters.Configurations;

public class CollateralBackfillReportConfiguration : IEntityTypeConfiguration<CollateralBackfillReport>
{
    public void Configure(EntityTypeBuilder<CollateralBackfillReport> builder)
    {
        builder.ToTable("CollateralBackfillReports");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.AppraisalId).IsRequired();
        builder.Property(r => r.Status).IsRequired().HasMaxLength(30);
        builder.Property(r => r.Message).HasMaxLength(1000);
        builder.Property(r => r.RunAt).IsRequired();

        builder.HasIndex(r => new { r.Status, r.RunAt })
            .HasDatabaseName("IX_CollateralBackfillReports_Status_RunAt");
    }
}
