using Collateral.CollateralMasters.Models;

namespace Collateral.CollateralMasters.Configurations;

public class CollateralResultLogConfiguration : IEntityTypeConfiguration<CollateralResultLog>
{
    public void Configure(EntityTypeBuilder<CollateralResultLog> builder)
    {
        builder.ToTable("CollateralResultLogs");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.AppraisalId).IsRequired();
        builder.Property(r => r.AppraisalNumber).IsRequired().HasMaxLength(50);
        builder.Property(r => r.CollateralId).IsRequired().HasMaxLength(19);
        builder.Property(r => r.SentAt).IsRequired();
        builder.Property(r => r.FileName).IsRequired().HasMaxLength(100);

        // Idempotency guard: one row per appraisal.
        builder.HasIndex(r => r.AppraisalId)
            .IsUnique()
            .HasDatabaseName("UX_CollateralResultLogs_Appraisal");
    }
}
