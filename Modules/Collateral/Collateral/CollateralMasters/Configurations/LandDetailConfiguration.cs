using Collateral.CollateralMasters.Models;

namespace Collateral.CollateralMasters.Configurations;

public class LandDetailConfiguration : IEntityTypeConfiguration<LandDetail>
{
    public void Configure(EntityTypeBuilder<LandDetail> builder)
    {
        builder.ToTable("LandDetails");

        builder.HasKey(d => d.CollateralMasterId);

        // Dedup key columns
        builder.Property(d => d.LandOfficeCode).IsRequired().HasMaxLength(20);
        builder.Property(d => d.Province).IsRequired().HasMaxLength(100);
        builder.Property(d => d.District).IsRequired().HasMaxLength(100);
        builder.Property(d => d.SubDistrict).IsRequired().HasMaxLength(100);
        builder.Property(d => d.TitleType).IsRequired().HasMaxLength(20);
        builder.Property(d => d.TitleNumber).IsRequired().HasMaxLength(50);
        builder.Property(d => d.SurveyNumber).HasMaxLength(50);
        builder.Property(d => d.LandParcelNumber).HasMaxLength(50);

        // Address (owned — flat columns)
        builder.OwnsOne(d => d.Address, a =>
        {
            a.Property(x => x.Street).HasColumnName("Street").HasMaxLength(200);
            a.Property(x => x.Village).HasColumnName("Village").HasMaxLength(200);
        });

        // Coordinates (owned — flat columns)
        builder.OwnsOne(d => d.Coordinates, c =>
        {
            c.Property(x => x.Latitude).HasColumnName("Latitude").HasPrecision(9, 6);
            c.Property(x => x.Longitude).HasColumnName("Longitude").HasPrecision(9, 6);
        });

        // Last-known land context
        builder.Property(d => d.LandShapeType).HasMaxLength(50);
        builder.Property(d => d.LandZoneType).HasMaxLength(50);
        builder.Property(d => d.UrbanPlanningType).HasMaxLength(50);
        builder.Property(d => d.AccessRoadWidth).HasPrecision(10, 2);
        builder.Property(d => d.RoadFrontage).HasPrecision(10, 2);
        builder.Property(d => d.LandArea).HasPrecision(18, 4);

        // Construction tracking
        builder.Property(d => d.IsUnderConstructionAtLastAppraisal).IsRequired().HasDefaultValue(false);
        builder.Property(d => d.OverallConstructionProgressPercent).HasPrecision(7, 4);

        // Three-value model (Phase C)
        builder.Property(d => d.UnitPrice).HasPrecision(18, 2);
        builder.Property(d => d.BuildingCost).HasPrecision(18, 2);
        builder.Property(d => d.AppraisalValue).HasPrecision(18, 2);

        // AppraisalSummary (owned — flat columns)
        builder.OwnsOne(d => d.AppraisalSummary, s =>
        {
            s.Property(x => x.LastAppraisalId).HasColumnName("LastAppraisalId");
            s.Property(x => x.LastAppraisalNumber).HasColumnName("LastAppraisalNumber").HasMaxLength(50);
            s.Property(x => x.LastAppraisedDate).HasColumnName("LastAppraisedDate");
        });

        builder.Property(d => d.IsDeleted).IsRequired().HasDefaultValue(false);

        // Filtered unique index for dedup — uses IsDeleted on THIS table (denormalized from master)
        builder.HasIndex(d => new
            {
                d.LandOfficeCode, d.Province, d.District, d.SubDistrict,
                d.TitleType, d.TitleNumber, d.SurveyNumber, d.LandParcelNumber
            })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_LandDetails_DedupKey_Active");

        // Partial-key lookup support
        builder.HasIndex(d => new { d.LandOfficeCode, d.TitleNumber })
            .HasDatabaseName("IX_LandDetails_LandOffice_TitleNumber");

        // Analytics: under-construction filter
        builder.HasIndex(d => d.IsUnderConstructionAtLastAppraisal)
            .HasFilter("[IsUnderConstructionAtLastAppraisal] = 1")
            .HasDatabaseName("IX_LandDetails_UnderConstruction");
    }
}
