using System.Text.Json;

namespace Appraisal.Infrastructure.Configurations;

public class LandAppraisalDetailConfiguration : IEntityTypeConfiguration<LandAppraisalDetail>
{
    public void Configure(EntityTypeBuilder<LandAppraisalDetail> builder)
    {
        builder.ToTable("LandAppraisalDetails", "appraisal");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        // 1:1 with AppraisalProperties
        builder.HasIndex(e => e.AppraisalPropertyId).IsUnique();

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

        // Document Verification - LandCheckMethodType is List<string>?
        builder.Property(e => e.LandCheckMethodType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.LandCheckMethodTypeOther).HasMaxLength(200);

        // Location Details
        builder.Property(e => e.Street).HasMaxLength(200);
        builder.Property(e => e.Soi).HasMaxLength(100);
        builder.Property(e => e.DistanceFromMainRoad).HasPrecision(10, 2);
        builder.Property(e => e.Village).HasMaxLength(200);
        builder.Property(e => e.AddressLocation).HasMaxLength(500);

        // Land Characteristics - Multi-select fields with JSON conversion
        builder.Property(e => e.LandShapeType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");

        builder.Property(e => e.UrbanPlanningType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");

        builder.Property(e => e.LandZoneType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");

        builder.Property(e => e.PlotLocationType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.PlotLocationTypeOther).HasMaxLength(200);

        builder.Property(e => e.LandFillStatusType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.LandFillStatusTypeOther).HasMaxLength(200);

        builder.Property(e => e.LandFillPercent).HasPrecision(5, 2);
        builder.Property(e => e.SoilLevel).HasPrecision(10, 2);

        // Road Access
        builder.Property(e => e.AccessRoadWidth).HasPrecision(10, 2);
        builder.Property(e => e.RightOfWay).HasPrecision(10, 2);
        builder.Property(e => e.RoadFrontage).HasPrecision(10, 2);
        builder.Property(e => e.RoadPassInFrontOfLand).HasMaxLength(200);

        builder.Property(e => e.LandAccessibilityType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.LandAccessibilityRemark).HasMaxLength(500);

        builder.Property(e => e.RoadSurfaceType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.RoadSurfaceTypeOther).HasMaxLength(200);

        // Utilities - Multi-select fields with JSON conversion
        builder.Property(e => e.PublicUtilityType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.PublicUtilityTypeOther).HasMaxLength(200);

        builder.Property(e => e.LandUseType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.LandUseTypeOther).HasMaxLength(200);

        builder.Property(e => e.LandEntranceExitType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.LandEntranceExitTypeOther).HasMaxLength(200);

        builder.Property(e => e.TransportationAccessType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.TransportationAccessTypeOther).HasMaxLength(200);

        builder.Property(e => e.PropertyAnticipationType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");

        // Legal Restrictions
        builder.Property(e => e.ExpropriationRemark).HasMaxLength(500);
        builder.Property(e => e.ExpropriationLineRemark).HasMaxLength(500);
        builder.Property(e => e.RoyalDecree).HasMaxLength(200);
        builder.Property(e => e.EncroachmentRemark).HasMaxLength(500);
        builder.Property(e => e.EncroachmentArea).HasPrecision(18, 4);
        builder.Property(e => e.ElectricityDistance).HasPrecision(10, 2);
        builder.Property(e => e.LandlockedRemark).HasMaxLength(500);
        builder.Property(e => e.ForestBoundaryRemark).HasMaxLength(500);
        builder.Property(e => e.OtherLegalLimitations).HasMaxLength(500);

        builder.Property(e => e.EvictionStatusType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");
        builder.Property(e => e.EvictionStatusTypeOther).HasMaxLength(200);

        builder.Property(e => e.AllocationStatusType)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null))
            .HasColumnType("nvarchar(500)");

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

        // Relationship - FK to AppraisalProperty (1:1)
        builder.Property(e => e.AppraisalPropertyId).IsRequired();
    }
}
