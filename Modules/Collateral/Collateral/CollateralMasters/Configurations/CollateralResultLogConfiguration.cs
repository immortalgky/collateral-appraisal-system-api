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

        // Row key, by appraisal AND collateral id.
        //
        // It used to be AppraisalId alone, which structurally forbade more than one outbound row per
        // appraisal. A block project sends one row per financed unit, each with its own AS400 id, so
        // the key has to include the collateral id.
        //
        // It is NOT what decides whether an appraisal is sent again: vw_CollateralResultExport drops any
        // appraisal that has ANY row here, so one sent with a blank id (Auto Update 'N') is not resent
        // when AS400 later mints an id.
        builder.HasIndex(r => new { r.AppraisalId, r.CollateralId })
            .IsUnique()
            .HasDatabaseName("UX_CollateralResultLogs_Appraisal_Collateral");
    }
}
