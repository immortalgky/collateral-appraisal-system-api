using Collateral.CollateralMasters.Models;

namespace Collateral.CollateralMasters.Configurations;

public class MachineDetailConfiguration : IEntityTypeConfiguration<MachineDetail>
{
    public void Configure(EntityTypeBuilder<MachineDetail> builder)
    {
        builder.ToTable("MachineDetails");

        builder.HasKey(d => d.CollateralMasterId);

        // Dedup tier 1
        builder.Property(d => d.MachineRegistrationNo).HasMaxLength(50);

        // Dedup tier 2 (LocationOwner dropped per spec v1 decision 28)
        builder.Property(d => d.SerialNo).HasMaxLength(100);
        builder.Property(d => d.Brand).HasMaxLength(100);
        builder.Property(d => d.Model).HasMaxLength(100);
        builder.Property(d => d.Manufacturer).HasMaxLength(200);

        // Identity-extra & last-known
        builder.Property(d => d.EngineNo).HasMaxLength(100);
        builder.Property(d => d.ChassisNo).HasMaxLength(100);
        builder.Property(d => d.MachineCondition).HasMaxLength(50);
        builder.Property(d => d.MachineAge).HasPrecision(5, 2);

        // AppraisalSummary (owned — flat columns)
        builder.OwnsOne(d => d.AppraisalSummary, s =>
        {
            s.Property(x => x.LastAppraisalId).HasColumnName("LastAppraisalId");
            s.Property(x => x.LastAppraisalNumber).HasColumnName("LastAppraisalNumber").HasMaxLength(50);
            s.Property(x => x.LastAppraisedDate).HasColumnName("LastAppraisedDate");
            s.Property(x => x.LastAppraisedValue).HasColumnName("LastAppraisedValue").HasPrecision(18, 2);
        });

        builder.Property(d => d.IsDeleted).IsRequired().HasDefaultValue(false);

        // Filtered unique index — tier 1: when registration no is present
        builder.HasIndex(d => d.MachineRegistrationNo)
            .IsUnique()
            .HasFilter("[MachineRegistrationNo] IS NOT NULL AND [IsDeleted] = 0")
            .HasDatabaseName("UX_MachineDetails_RegistrationNo_Active");

        // Filtered unique index — tier 2: when registration no is absent
        builder.HasIndex(d => new { d.SerialNo, d.Brand, d.Model, d.Manufacturer })
            .IsUnique()
            .HasFilter("[MachineRegistrationNo] IS NULL AND [IsDeleted] = 0")
            .HasDatabaseName("UX_MachineDetails_Composite_Active");

        // Partial-key lookup
        builder.HasIndex(d => d.SerialNo)
            .HasDatabaseName("IX_MachineDetails_SerialNo");
    }
}
