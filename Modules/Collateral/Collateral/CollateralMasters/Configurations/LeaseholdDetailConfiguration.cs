using Collateral.CollateralMasters.Models;

namespace Collateral.CollateralMasters.Configurations;

public class LeaseholdDetailConfiguration : IEntityTypeConfiguration<LeaseholdDetail>
{
    public void Configure(EntityTypeBuilder<LeaseholdDetail> builder)
    {
        builder.ToTable("LeaseholdDetails");

        builder.HasKey(d => d.CollateralMasterId);

        // Dedup key columns
        builder.Property(d => d.LeaseRegistrationNo).IsRequired().HasMaxLength(50);
        builder.Property(d => d.UnderlyingMasterId).IsRequired();
        builder.Property(d => d.Lessor).IsRequired().HasMaxLength(200);
        builder.Property(d => d.Lessee).IsRequired().HasMaxLength(200);
        builder.Property(d => d.LeaseTermStart).IsRequired();

        // Last-known
        builder.Property(d => d.LeaseTermMonths);

        // AppraisalSummary (owned — flat columns)
        builder.OwnsOne(d => d.AppraisalSummary, s =>
        {
            s.Property(x => x.LastAppraisalId).HasColumnName("LastAppraisalId");
            s.Property(x => x.LastAppraisalNumber).HasColumnName("LastAppraisalNumber").HasMaxLength(50);
            s.Property(x => x.LastAppraisedDate).HasColumnName("LastAppraisedDate");
        });

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
