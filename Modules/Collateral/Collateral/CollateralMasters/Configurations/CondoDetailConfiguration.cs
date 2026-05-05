using Collateral.CollateralMasters.Models;

namespace Collateral.CollateralMasters.Configurations;

public class CondoDetailConfiguration : IEntityTypeConfiguration<CondoDetail>
{
    public void Configure(EntityTypeBuilder<CondoDetail> builder)
    {
        builder.ToTable("CondoDetails");

        builder.HasKey(d => d.CollateralMasterId);

        // Dedup key columns (7 columns per spec v1 section 5.5)
        builder.Property(d => d.LandOfficeCode).IsRequired().HasMaxLength(200);
        builder.Property(d => d.CondoRegistrationNumber).IsRequired().HasMaxLength(200);
        builder.Property(d => d.BuildingNumber).IsRequired().HasMaxLength(50);
        builder.Property(d => d.FloorNumber).IsRequired().HasMaxLength(50);
        builder.Property(d => d.UnitNumber).IsRequired().HasMaxLength(50);
        builder.Property(d => d.TitleNumber).IsRequired().HasMaxLength(50);
        builder.Property(d => d.TitleType).IsRequired().HasMaxLength(20);

        // Identity-extra
        builder.Property(d => d.CondoName).HasMaxLength(200);
        builder.Property(d => d.Province).HasMaxLength(100);

        // Last-known
        builder.Property(d => d.UsableArea).HasPrecision(18, 4);
        builder.Property(d => d.LocationType).HasMaxLength(50);
        builder.Property(d => d.ModelName).HasMaxLength(200);

        // AppraisalSummary (owned — flat columns)
        builder.OwnsOne(d => d.AppraisalSummary, s =>
        {
            s.Property(x => x.LastAppraisalId).HasColumnName("LastAppraisalId");
            s.Property(x => x.LastAppraisalNumber).HasColumnName("LastAppraisalNumber").HasMaxLength(50);
            s.Property(x => x.LastAppraisedDate).HasColumnName("LastAppraisedDate");
            s.Property(x => x.LastAppraisedValue).HasColumnName("LastAppraisedValue").HasPrecision(18, 2);
        });

        builder.Property(d => d.IsDeleted).IsRequired().HasDefaultValue(false);

        // Filtered unique index for dedup
        builder.HasIndex(d => new
            {
                d.LandOfficeCode, d.CondoRegistrationNumber, d.BuildingNumber,
                d.FloorNumber, d.UnitNumber, d.TitleNumber, d.TitleType
            })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_CondoDetails_DedupKey_Active");

        // Partial-key lookup
        builder.HasIndex(d => new { d.LandOfficeCode, d.TitleNumber, d.TitleType })
            .HasDatabaseName("IX_CondoDetails_LandOffice_TitleNumber_TitleType");
    }
}
