using Collateral.CollateralMasters.Models;

namespace Collateral.CollateralMasters.Configurations;

public class CondoDetailConfiguration : IEntityTypeConfiguration<CondoDetail>
{
    public void Configure(EntityTypeBuilder<CondoDetail> builder)
    {
        builder.ToTable("CondoDetails");

        builder.HasKey(d => d.CollateralMasterId);

        // Dedup key columns (7): CondoRegistrationNumber + BuildingNumber + FloorNumber + RoomNumber
        //                      + Province + District + SubDistrict.
        // LandOfficeCode is descriptive (nullable), NOT part of the key. TitleNumber/TitleType dropped.
        builder.Property(d => d.LandOfficeCode).HasMaxLength(200);
        builder.Property(d => d.CondoRegistrationNumber).IsRequired().HasMaxLength(200);
        builder.Property(d => d.BuildingNumber).IsRequired().HasMaxLength(50);
        builder.Property(d => d.FloorNumber).IsRequired().HasMaxLength(50);
        builder.Property(d => d.RoomNumber).IsRequired().HasMaxLength(50).HasColumnName("RoomNumber");
        builder.Property(d => d.Province).IsRequired().HasMaxLength(100);
        builder.Property(d => d.District).IsRequired().HasMaxLength(100);
        builder.Property(d => d.SubDistrict).IsRequired().HasMaxLength(100);

        // Identity-extra
        builder.Property(d => d.CondoName).HasMaxLength(200);

        // Last-known
        builder.Property(d => d.UsableArea).HasPrecision(18, 4);
        builder.Property(d => d.LocationType).HasMaxLength(50);
        builder.Property(d => d.ModelName).HasMaxLength(200);

        // GPS coordinates (Phase 1 — geo filter prerequisite)
        builder.Property(d => d.Latitude).HasPrecision(9, 6);
        builder.Property(d => d.Longitude).HasPrecision(9, 6);

        // Three-value model (Phase C)
        builder.Property(d => d.UnitPrice).HasPrecision(18, 2);
        builder.Property(d => d.BuildingValue).HasPrecision(18, 2);
        builder.Property(d => d.AppraisalValue).HasPrecision(18, 2);

        // AppraisalSummary (owned — flat columns)
        builder.OwnsOne(d => d.AppraisalSummary, s =>
        {
            s.Property(x => x.LastAppraisalId).HasColumnName("LastAppraisalId");
            s.Property(x => x.LastAppraisalNumber).HasColumnName("LastAppraisalNumber").HasMaxLength(50);
            s.Property(x => x.LastAppraisedDate).HasColumnName("LastAppraisedDate");
        });

        builder.Property(d => d.IsDeleted).IsRequired().HasDefaultValue(false);

        // Filtered unique index for dedup (7-column key)
        builder.HasIndex(d => new
            {
                d.CondoRegistrationNumber, d.BuildingNumber,
                d.FloorNumber, d.RoomNumber,
                d.Province, d.District, d.SubDistrict
            })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_CondoDetails_DedupKey_Active");
    }
}
