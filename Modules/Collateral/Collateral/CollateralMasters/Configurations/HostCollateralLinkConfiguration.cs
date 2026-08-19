using Collateral.CollateralMasters.Models;

namespace Collateral.CollateralMasters.Configurations;

public class HostCollateralLinkConfiguration : IEntityTypeConfiguration<HostCollateralLink>
{
    public void Configure(EntityTypeBuilder<HostCollateralLink> builder)
    {
        builder.ToTable("HostCollateralLinks");

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedNever();

        builder.Property(h => h.AppraisalNumber).IsRequired().HasMaxLength(50);
        // dec(19) in the AS400 record; stored as text because it is an identifier, not a number.
        builder.Property(h => h.HostCollateralId).HasMaxLength(19);
        builder.Property(h => h.IsRedeemed).IsRequired();
        builder.Property(h => h.UpdatedAt).IsRequired();

        // One row per appraisal number — the grain AS400 sends. Also the lookup the v2 regulatory
        // view joins on, so it has to be unique for the join not to fan out.
        builder.HasIndex(h => h.AppraisalNumber)
            .IsUnique()
            .HasDatabaseName("UX_HostCollateralLinks_AppraisalNumber");

        // The v2 view filters redeemed rows out; the id lookup is by appraisal number above.
        builder.HasIndex(h => h.IsRedeemed)
            .HasDatabaseName("IX_HostCollateralLinks_IsRedeemed");
    }
}
