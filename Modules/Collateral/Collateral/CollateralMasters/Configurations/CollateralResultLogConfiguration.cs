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

        // Idempotency guard, keyed by appraisal AND collateral id.
        //
        // It used to be AppraisalId alone, which structurally forbade more than one outbound row per
        // appraisal. That held while the file was one row per collateral master; it does not hold now
        // that a block project sends one row per financed unit, each with its own AS400 id.
        //
        // It also gives an appraisal sent WITHOUT an id (CollateralId '', Auto Update 'N') a second
        // chance: once AS400 mints the id at drawdown, the same appraisal pairs with a new key and
        // goes out again — this time carrying the id. Under the old index that appraisal was marked
        // done forever and never got its id across.
        builder.HasIndex(r => new { r.AppraisalId, r.CollateralId })
            .IsUnique()
            .HasDatabaseName("UX_CollateralResultLogs_Appraisal_Collateral");
    }
}
