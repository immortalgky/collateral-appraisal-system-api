using Collateral.CollateralMasters.Models;

namespace Collateral.CollateralMasters.Configurations;

public class LandDetailConfiguration : IEntityTypeConfiguration<LandDetail>
{
    public void Configure(EntityTypeBuilder<LandDetail> builder)
    {
        builder.ToTable("LandDetails");

        builder.HasKey(d => d.CollateralMasterId);

        // Widths mirror the appraisal columns these are copied from. Nothing on the write path trims or
        // validates length, so a collateral column narrower than its source turns an over-length value
        // into SqlException 8152 inside SaveChangesAsync — which CollateralMasterUpsertService does not
        // catch, so it dead-letters AppraisalCompletedConsumer.

        // LandOfficeCode ← appraisal.LandAppraisalDetails.LandOffice nvarchar(200) NULL.
        // Descriptive, NOT part of the dedup key, and GetMissingFields deliberately does not require it —
        // so it must be nullable here too. It was NOT NULL(20), which is what made seven backfill runs
        // fail with "Cannot insert the value NULL into column 'LandOfficeCode'".
        builder.Property(d => d.LandOfficeCode).HasMaxLength(200);

        // Dedup key columns. Province/District/SubDistrict stay required: GetMissingFields rejects a
        // property that lacks any of them before it can reach an INSERT.
        builder.Property(d => d.Province).IsRequired().HasMaxLength(100);
        builder.Property(d => d.District).IsRequired().HasMaxLength(100);
        builder.Property(d => d.SubDistrict).IsRequired().HasMaxLength(100);

        // TitleType left the dedup key on 2026-08-09, so it is no longer identity and no longer required.
        builder.Property(d => d.TitleType).HasMaxLength(50);
        builder.Property(d => d.TitleNumber).IsRequired().HasMaxLength(200);
        builder.Property(d => d.SurveyNumber).HasMaxLength(50);
        builder.Property(d => d.LandParcelNumber).HasMaxLength(50);
        builder.Property(d => d.Rawang).HasMaxLength(100);

        // Address (owned — flat columns)
        builder.OwnsOne(d => d.Address, a =>
        {
            a.Property(x => x.Street).HasColumnName("Street").HasMaxLength(200);
            a.Property(x => x.Village).HasColumnName("Village").HasMaxLength(200);
        });

        // Coordinates (owned — flat columns)
        builder.OwnsOne(d => d.Coordinates, c =>
        {
            // Deliberately left at (9,6) even though appraisal stores (10,7). Widening means dropping
            // and rebuilding the persisted computed column GeoPoint and its spatial index
            // (IX_LandDetails_GeoPoint) — SQL Server error 5074 otherwise. The gap costs about a
            // centimetre of precision on copy and never throws, which does not justify that on a live
            // table. Same reasoning in CondoDetailConfiguration and ProjectDetailConfiguration.
            c.Property(x => x.Latitude).HasColumnName("Latitude").HasPrecision(9, 6);
            c.Property(x => x.Longitude).HasColumnName("Longitude").HasPrecision(9, 6);
        });

        // Last-known land context — widths from appraisal.LandAppraisalDetails
        builder.Property(d => d.LandShapeType).HasMaxLength(100);
        builder.Property(d => d.LandZoneType).HasMaxLength(500);
        builder.Property(d => d.UrbanPlanningType).HasMaxLength(100);
        builder.Property(d => d.AccessRoadWidth).HasPrecision(10, 2);
        builder.Property(d => d.RoadFrontage).HasPrecision(10, 2);
        builder.Property(d => d.LandArea).HasPrecision(18, 4);

        // Construction tracking

        // Three-value model (Phase C)

        // AppraisalSummary (owned — flat columns)

        builder.Property(d => d.IsDeleted).IsRequired().HasDefaultValue(false);

        // Filtered unique index for dedup — uses IsDeleted on THIS table (denormalized from master)
        // Dedup key — narrowed from eight columns to four on 2026-08-09.
        // TitleType / SurveyNumber / LandParcelNumber / Rawang were splitting one physical parcel
        // across several masters whenever an appraiser recorded them differently.
        // See CollateralMasterRepository.LandKeyMatches; CollateralMasterUpsertService.BuildTitleKey
        // must stay in step.
        builder.HasIndex(d => new { d.Province, d.District, d.SubDistrict, d.TitleNumber })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_LandDetails_DedupKey_Active");

        // Partial-key lookup support
        builder.HasIndex(d => new { d.LandOfficeCode, d.TitleNumber })
            .HasDatabaseName("IX_LandDetails_LandOffice_TitleNumber");

        // Analytics: under-construction filter
    }
}
