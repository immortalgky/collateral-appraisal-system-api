using Collateral.CollateralMasters.Models;

namespace Collateral.CollateralMasters.Configurations;

public class HostCollateralLinkConfiguration : IEntityTypeConfiguration<HostCollateralLink>
{
    public void Configure(EntityTypeBuilder<HostCollateralLink> builder)
    {
        builder.ToTable("HostCollateralLinks");

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedNever();

        // dec(19) in the AS400 record; stored as text because it is an identifier, not a number.
        builder.Property(h => h.HostCollateralId).IsRequired().HasMaxLength(19);
        builder.Property(h => h.AppraisalNumber).IsRequired().HasMaxLength(50);
        builder.Property(h => h.CollateralName).HasMaxLength(40);
        builder.Property(h => h.LocationCode).HasMaxLength(6);
        builder.Property(h => h.CollateralCode).HasMaxLength(3);
        builder.Property(h => h.PropertyType).HasMaxLength(3);
        builder.Property(h => h.PropertyTypeDesc).HasMaxLength(40);
        builder.Property(h => h.IsRedeemed).IsRequired();
        builder.Property(h => h.MasterTitle).HasMaxLength(1);
        builder.Property(h => h.UpdatedAt).IsRequired();

        // One row per COLLATERAL — the grain AS400 actually sends. This index was previously on
        // AppraisalNumber, which forced the ingest to collapse an appraisal's several collateral into
        // one row and dropped 23% of the feed. AppraisalNumber is deliberately NOT unique now.
        builder.HasIndex(h => h.HostCollateralId)
            .IsUnique()
            .HasDatabaseName("UX_HostCollateralLinks_HostCollateralId");

        // Still the join used to reach an appraisal from a collateral id.
        builder.HasIndex(h => h.AppraisalNumber)
            .HasDatabaseName("IX_HostCollateralLinks_AppraisalNumber");

        // The regulatory export selects on both flags together: held AND a stated master-title flag.
        builder.HasIndex(h => new { h.IsRedeemed, h.MasterTitle })
            .HasDatabaseName("IX_HostCollateralLinks_IsRedeemed_MasterTitle");
    }
}
