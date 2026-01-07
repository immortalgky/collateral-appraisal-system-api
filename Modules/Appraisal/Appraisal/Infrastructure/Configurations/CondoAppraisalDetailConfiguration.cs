namespace Appraisal.Infrastructure.Configurations;

public class CondoAppraisalDetailConfiguration : IEntityTypeConfiguration<CondoAppraisalDetail>
{
    public void Configure(EntityTypeBuilder<CondoAppraisalDetail> builder)
    {
        builder.ToTable("CondoAppraisalDetails", "appraisal");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        // 1:1 with AppraisalProperties
        builder.HasIndex(e => e.AppraisalPropertyId).IsUnique();

        // Property Identification
        builder.Property(e => e.PropertyName).HasMaxLength(200);
        builder.Property(e => e.CondoName).HasMaxLength(200);
        builder.Property(e => e.BuildingNumber).HasMaxLength(50);
        builder.Property(e => e.ModelName).HasMaxLength(100);
        builder.Property(e => e.BuiltOnTitleNo).HasMaxLength(100);
        builder.Property(e => e.CondoRegisNo).HasMaxLength(100);
        builder.Property(e => e.RoomNo).HasMaxLength(50);
        builder.Property(e => e.UsableArea).HasPrecision(18, 4);

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
        builder.Property(e => e.OwnerName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.BuildingCondition).HasMaxLength(50);
        builder.Property(e => e.ObligationDetails).HasMaxLength(500);

        // Location Details
        builder.Property(e => e.CondoLocation).HasMaxLength(500);
        builder.Property(e => e.Street).HasMaxLength(200);
        builder.Property(e => e.Soi).HasMaxLength(100);
        builder.Property(e => e.DistanceFromMainRoad).HasPrecision(10, 2);
        builder.Property(e => e.AccessRoadWidth).HasPrecision(10, 2);
        builder.Property(e => e.RightOfWay).HasMaxLength(100);
        builder.Property(e => e.RoadSurfaceType).HasMaxLength(100);
        builder.Property(e => e.PublicUtility).HasMaxLength(500);
        builder.Property(e => e.PublicUtilityOther).HasMaxLength(200);

        // Building Info
        builder.Property(e => e.Decoration).HasMaxLength(100);
        builder.Property(e => e.DecorationOther).HasMaxLength(200);
        builder.Property(e => e.BuildingForm).HasMaxLength(100);
        builder.Property(e => e.ConstMaterial).HasMaxLength(100);

        // Layout & Materials
        builder.Property(e => e.RoomLayout).HasMaxLength(100);
        builder.Property(e => e.RoomLayoutOther).HasMaxLength(200);
        builder.Property(e => e.LocationView).HasMaxLength(200);
        builder.Property(e => e.GroundFloorMaterial).HasMaxLength(100);
        builder.Property(e => e.GroundFloorMaterialOther).HasMaxLength(200);
        builder.Property(e => e.UpperFloorMaterial).HasMaxLength(100);
        builder.Property(e => e.UpperFloorMaterialOther).HasMaxLength(200);
        builder.Property(e => e.BathroomFloorMaterial).HasMaxLength(100);
        builder.Property(e => e.BathroomFloorMaterialOther).HasMaxLength(200);
        builder.Property(e => e.Roof).HasMaxLength(100);
        builder.Property(e => e.RoofOther).HasMaxLength(200);

        // Area
        builder.Property(e => e.TotalBuildingArea).HasPrecision(18, 4);

        // Legal Restrictions
        builder.Property(e => e.ExpropriationRemark).HasMaxLength(500);
        builder.Property(e => e.ExpropriationLineRemark).HasMaxLength(500);
        builder.Property(e => e.RoyalDecree).HasMaxLength(200);
        builder.Property(e => e.ForestBoundaryRemark).HasMaxLength(500);

        // Facilities & Environment
        builder.Property(e => e.CondoFacility).HasMaxLength(500);
        builder.Property(e => e.CondoFacilityOther).HasMaxLength(200);
        builder.Property(e => e.Environment).HasMaxLength(500);

        // Pricing
        builder.Property(e => e.BuildingInsurancePrice).HasPrecision(18, 2);
        builder.Property(e => e.SellingPrice).HasPrecision(18, 2);
        builder.Property(e => e.ForcedSalePrice).HasPrecision(18, 2);

        // Other
        builder.Property(e => e.Remark).HasMaxLength(1000);

        // Relationship - FK to AppraisalProperty (1:1)
        builder.Property(e => e.AppraisalPropertyId).IsRequired();
    }
}
