namespace Appraisal.Infrastructure.Configurations;

public class LeaseAgreementDetailConfiguration : IOwnedEntityConfiguration<AppraisalProperty, LeaseAgreementDetail>
{
    public void Configure(OwnedNavigationBuilder<AppraisalProperty, LeaseAgreementDetail> builder)
    {
        builder.ToTable("LeaseAgreementDetails", "appraisal");
        builder.WithOwner().HasForeignKey(e => e.AppraisalPropertyId);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        // 1:1 with AppraisalProperties
        builder.HasIndex(e => e.AppraisalPropertyId).IsUnique();

        // Information
        builder.Property(e => e.LesseeName).HasMaxLength(200);
        builder.Property(e => e.TenantName).HasMaxLength(200);

        // Contract
        builder.Property(e => e.LeasePeriodAsContract).HasMaxLength(200);
        builder.Property(e => e.RemainingLeaseAsAppraisalDate).HasMaxLength(200);
        builder.Property(e => e.ContractNo).HasMaxLength(100);

        // Fees
        builder.Property(e => e.LeaseRentFee).HasPrecision(18, 2);
        builder.Property(e => e.RentAdjust).HasPrecision(18, 2);

        // Terms
        builder.Property(e => e.Sublease).HasMaxLength(500);
        builder.Property(e => e.AdditionalExpenses).HasMaxLength(500);
        builder.Property(e => e.LeaseTimestamp).HasMaxLength(200);
        builder.Property(e => e.ContractRenewal).HasMaxLength(500);

        // Long text
        builder.Property(e => e.RentalTermsImpactingPropertyUse).HasMaxLength(2000);
        builder.Property(e => e.TerminationOfLease).HasMaxLength(2000);

        // Other
        builder.Property(e => e.Remark).HasMaxLength(1000);
        builder.Property(e => e.Banking).HasMaxLength(200);

        builder.Property(e => e.AppraisalPropertyId).IsRequired();
    }
}
