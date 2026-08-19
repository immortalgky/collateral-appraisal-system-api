using Collateral.CollateralMasters.Models;

namespace Collateral.CollateralMasters.Configurations;

public class LeaseholdDetailConfiguration : IEntityTypeConfiguration<LeaseholdDetail>
{
    public void Configure(EntityTypeBuilder<LeaseholdDetail> builder)
    {
        builder.ToTable("LeaseholdDetails");

        builder.HasKey(d => d.CollateralMasterId);

        // Dedup key columns
        // ← appraisal.LeaseAgreementDetails.ContractNo nvarchar(100) — renamed on the way over, so a
        // same-name schema comparison never spots the width gap.
        builder.Property(d => d.LeaseRegistrationNo).IsRequired().HasMaxLength(100);
        builder.Property(d => d.UnderlyingMasterId).IsRequired();
        builder.Property(d => d.Lessor).IsRequired().HasMaxLength(200);
        builder.Property(d => d.Lessee).IsRequired().HasMaxLength(200);
        builder.Property(d => d.LeaseTermStart).IsRequired();

        // Last-known
        builder.Property(d => d.LeaseTermMonths);

        // Appraisal-level total from the latest appraisal (IsMaster-only). Mirrors Land/Condo detail.

        // AppraisalSummary (owned — flat columns)

        builder.Property(d => d.IsDeleted).IsRequired().HasDefaultValue(false);

        // FK to underlying master — RESTRICT delete (can't delete underlying that has leaseholds)
        builder.HasOne<CollateralMaster>()
            .WithMany()
            .HasForeignKey(d => d.UnderlyingMasterId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LeaseholdDetails_UnderlyingMaster");

        // Filtered unique index for dedup
        builder.HasIndex(d => new
            {
                d.LeaseRegistrationNo, d.UnderlyingMasterId, d.Lessor,
                d.Lessee, d.LeaseTermStart
            })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_LeaseholdDetails_DedupKey_Active");

        // Supporting indexes
        builder.HasIndex(d => d.UnderlyingMasterId)
            .HasDatabaseName("IX_LeaseholdDetails_UnderlyingMasterId");

        builder.HasIndex(d => d.LeaseRegistrationNo)
            .HasDatabaseName("IX_LeaseholdDetails_LeaseRegistrationNo");
    }
}
