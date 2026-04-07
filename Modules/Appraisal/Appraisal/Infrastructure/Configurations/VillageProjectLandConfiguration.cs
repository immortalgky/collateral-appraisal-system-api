using System.Text.Json;

namespace Appraisal.Infrastructure.Configurations;

public class VillageProjectLandConfiguration : IEntityTypeConfiguration<VillageProjectLand>
{
    public void Configure(EntityTypeBuilder<VillageProjectLand> builder)
    {
        builder.ToTable("VillageProjectLands");

        // Primary Key
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        // Foreign Key (1:1 with Appraisal)
        builder.Property(e => e.AppraisalId).IsRequired();
        builder.HasIndex(e => e.AppraisalId).IsUnique();

        // Property Identification
        builder.Property(e => e.PropertyName).HasMaxLength(200);
        builder.Property(e => e.LandDescription).HasMaxLength(500);

        // GPS Coordinates (Value Object)
        builder.OwnsOne(e => e.Coordinates, coord =>
        {
            coord.Property(c => c.Latitude).HasColumnName("Latitude").HasPrecision(10, 7);
            coord.Property(c => c.Longitude).HasColumnName("Longitude").HasPrecision(10, 7);
        });

        // Administrative Address (Value Object)
        builder.OwnsOne(e => e.Address, addr =>
        {
            addr.Property(a => a.SubDistrict).HasColumnName("SubDistrict").HasMaxLength(100);
            addr.Property(a => a.District).HasColumnName("District").HasMaxLength(100);
            addr.Property(a => a.Province).HasColumnName("Province").HasMaxLength(100);
            addr.Property(a => a.LandOffice).HasColumnName("LandOffice").HasMaxLength(200);
        });

        // Owner
        builder.Property(e => e.OwnerName).HasMaxLength(200);
        builder.Property(e => e.ObligationDetails).HasMaxLength(500);

        // Document Verification
        builder.Property(e => e.LandCheckMethodType).HasMaxLength(100);
        builder.Property(e => e.LandCheckMethodTypeOther).HasMaxLength(200);

        // Location Details
        builder.Property(e => e.Street).HasMaxLength(200);
        builder.Property(e => e.Soi).HasMaxLength(100);
        builder.Property(e => e.DistanceFromMainRoad).HasPrecision(10, 2);
        builder.Property(e => e.Village).HasMaxLength(200);
        builder.Property(e => e.AddressLocation).HasMaxLength(500);

        // Land Characteristics
        builder.Property(e => e.LandShapeType).HasMaxLength(100);
        builder.Property(e => e.UrbanPlanningType).HasMaxLength(100);
        builder.Property(e => e.LandZoneType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.LandZoneTypeOther).HasMaxLength(200);

        builder.Property(e => e.PlotLocationType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.PlotLocationTypeOther).HasMaxLength(200);

        builder.Property(e => e.LandFillType).HasMaxLength(100);
        builder.Property(e => e.LandFillTypeOther).HasMaxLength(200);
        builder.Property(e => e.LandFillPercent).HasPrecision(5, 2);
        builder.Property(e => e.SoilLevel).HasPrecision(10, 2);

        // Road Access
        builder.Property(e => e.AccessRoadWidth).HasPrecision(10, 2);
        builder.Property(e => e.RoadFrontage).HasPrecision(10, 2);
        builder.Property(e => e.RoadPassInFrontOfLand).HasMaxLength(200);
        builder.Property(e => e.LandAccessibilityType).HasMaxLength(100);
        builder.Property(e => e.LandAccessibilityRemark).HasMaxLength(500);
        builder.Property(e => e.RoadSurfaceType).HasMaxLength(100);
        builder.Property(e => e.RoadSurfaceTypeOther).HasMaxLength(200);

        // Utilities
        builder.Property(e => e.ElectricityDistance).HasPrecision(10, 2);
        builder.Property(e => e.PublicUtilityType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.PublicUtilityTypeOther).HasMaxLength(200);

        builder.Property(e => e.LandUseType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.LandUseTypeOther).HasMaxLength(200);

        builder.Property(e => e.LandEntranceExitType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.LandEntranceExitTypeOther).HasMaxLength(200);

        builder.Property(e => e.TransportationAccessType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.TransportationAccessTypeOther).HasMaxLength(200);
        builder.Property(e => e.PropertyAnticipationType).HasMaxLength(100);
        builder.Property(e => e.PropertyAnticipationTypeOther).HasMaxLength(200);

        // Legal Restrictions
        builder.Property(e => e.ExpropriationRemark).HasMaxLength(500);
        builder.Property(e => e.ExpropriationLineRemark).HasMaxLength(500);
        builder.Property(e => e.RoyalDecree).HasMaxLength(500);
        builder.Property(e => e.EncroachmentRemark).HasMaxLength(500);
        builder.Property(e => e.EncroachmentArea).HasPrecision(18, 4);
        builder.Property(e => e.LandlockedRemark).HasMaxLength(500);
        builder.Property(e => e.ForestBoundaryRemark).HasMaxLength(500);
        builder.Property(e => e.OtherLegalLimitations).HasMaxLength(1000);

        builder.Property(e => e.EvictionType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.EvictionTypeOther).HasMaxLength(200);
        builder.Property(e => e.AllocationType).HasMaxLength(100);

        // Adjacent Boundaries
        builder.Property(e => e.NorthAdjacentArea).HasMaxLength(200);
        builder.Property(e => e.NorthBoundaryLength).HasPrecision(10, 2);
        builder.Property(e => e.SouthAdjacentArea).HasMaxLength(200);
        builder.Property(e => e.SouthBoundaryLength).HasPrecision(10, 2);
        builder.Property(e => e.EastAdjacentArea).HasMaxLength(200);
        builder.Property(e => e.EastBoundaryLength).HasPrecision(10, 2);
        builder.Property(e => e.WestAdjacentArea).HasMaxLength(200);
        builder.Property(e => e.WestBoundaryLength).HasPrecision(10, 2);

        // Other Features
        builder.Property(e => e.PondArea).HasPrecision(18, 4);
        builder.Property(e => e.PondDepth).HasPrecision(10, 2);
        builder.Property(e => e.HasBuildingOther).HasMaxLength(200);
        builder.Property(e => e.Remark).HasMaxLength(1000);

        // VillageProjectLandTitle (owned collection)
        builder.OwnsMany(e => e.Titles, title =>
        {
            title.ToTable("VillageProjectLandTitles");
            title.WithOwner().HasForeignKey(t => t.VillageProjectLandId);
            title.HasKey(t => t.Id);
            title.Property(t => t.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

            title.Property(t => t.TitleNumber).IsRequired().HasMaxLength(100);
            title.Property(t => t.TitleType).IsRequired().HasMaxLength(50);
            title.Property(t => t.BookNumber).HasMaxLength(50);
            title.Property(t => t.PageNumber).HasMaxLength(50);
            title.Property(t => t.LandParcelNumber).HasMaxLength(50);
            title.Property(t => t.SurveyNumber).HasMaxLength(50);
            title.Property(t => t.MapSheetNumber).HasMaxLength(50);
            title.Property(t => t.Rawang).HasMaxLength(100);
            title.Property(t => t.AerialMapName).HasMaxLength(200);
            title.Property(t => t.AerialMapNumber).HasMaxLength(100);

            // Area (Value Object)
            title.OwnsOne(t => t.Area, area =>
            {
                area.Property(a => a.Rai).HasColumnName("AreaRai").HasPrecision(10, 2);
                area.Property(a => a.Ngan).HasColumnName("AreaNgan").HasPrecision(10, 2);
                area.Property(a => a.SquareWa).HasColumnName("AreaSquareWa").HasPrecision(10, 2);
            });

            title.Property(t => t.BoundaryMarkerType).HasMaxLength(100);
            title.Property(t => t.BoundaryMarkerRemark).HasMaxLength(500);
            title.Property(t => t.DocumentValidationResultType).HasMaxLength(100);
            title.Property(t => t.GovernmentPricePerSqWa).HasPrecision(18, 2);
            title.Property(t => t.GovernmentPrice).HasPrecision(18, 2);
            title.Property(t => t.Remark).HasMaxLength(1000);

            title.HasIndex(t => t.VillageProjectLandId);
        });

        // Backing field for owned collection
        builder.Navigation(e => e.Titles)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
