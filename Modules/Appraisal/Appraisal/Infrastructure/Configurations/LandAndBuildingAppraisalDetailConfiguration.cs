namespace Appraisal.Infrastructure.Configurations;

public class LandAndBuildingAppraisalDetailConfiguration : IEntityTypeConfiguration<LandAndBuildingAppraisalDetail>
{
    public void Configure(EntityTypeBuilder<LandAndBuildingAppraisalDetail> builder)
    {
        builder.ToTable("LandAndBuildingAppraisalDetails", "appraisal");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        // 1:1 with AppraisalProperties
        builder.HasIndex(e => e.AppraisalPropertyId).IsUnique();
        builder.Property(e => e.AppraisalPropertyId).IsRequired();

        // =====================================================
        // PROPERTY IDENTIFICATION
        // =====================================================
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

        // =====================================================
        // SHARED OWNER FIELDS
        // =====================================================
        builder.Property(e => e.OwnerName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.OwnershipType).IsRequired().HasMaxLength(50);
        builder.Property(e => e.OwnershipDocument).HasMaxLength(100);
        builder.Property(e => e.OwnershipPercentage).HasPrecision(5, 2);
        builder.Property(e => e.ObligationDetails).HasMaxLength(500);
        builder.Property(e => e.PropertyUsage).HasMaxLength(100);
        builder.Property(e => e.OccupancyStatus).HasMaxLength(50);

        // =====================================================
        // LAND SECTION - Title Deed Info
        // =====================================================
        builder.Property(e => e.TitleDeedType).HasMaxLength(50);
        builder.Property(e => e.TitleDeedNumber).HasMaxLength(100);
        builder.Property(e => e.LandNumber).HasMaxLength(50);
        builder.Property(e => e.SurveyPageNumber).HasMaxLength(50);

        // Land Area (Value Object)
        builder.OwnsOne(e => e.Area, area =>
        {
            area.Property(a => a.Rai).HasColumnName("LandAreaRai").HasPrecision(10, 2);
            area.Property(a => a.Ngan).HasColumnName("LandAreaNgan").HasPrecision(10, 2);
            area.Property(a => a.SquareWa).HasColumnName("LandAreaSqWa").HasPrecision(10, 2);
        });

        // =====================================================
        // LAND SECTION - Document Verification
        // =====================================================
        builder.Property(e => e.LandLocationVerification).HasMaxLength(200);
        builder.Property(e => e.LandCheckMethod).HasMaxLength(100);
        builder.Property(e => e.LandCheckMethodOther).HasMaxLength(200);

        // =====================================================
        // LAND SECTION - Location Details
        // =====================================================
        builder.Property(e => e.Street).HasMaxLength(200);
        builder.Property(e => e.Soi).HasMaxLength(100);
        builder.Property(e => e.DistanceFromMainRoad).HasPrecision(10, 2);
        builder.Property(e => e.Village).HasMaxLength(100);
        builder.Property(e => e.AddressLocation).HasMaxLength(500);

        // =====================================================
        // LAND SECTION - Land Characteristics
        // =====================================================
        builder.Property(e => e.LandShape).HasMaxLength(50);
        builder.Property(e => e.UrbanPlanningType).HasMaxLength(100);
        builder.Property(e => e.PlotLocation).HasMaxLength(100);
        builder.Property(e => e.PlotLocationOther).HasMaxLength(200);
        builder.Property(e => e.LandFillStatus).HasMaxLength(50);
        builder.Property(e => e.LandFillStatusOther).HasMaxLength(200);
        builder.Property(e => e.LandFillPercent).HasPrecision(5, 2);
        builder.Property(e => e.TerrainType).HasMaxLength(50);
        builder.Property(e => e.SoilCondition).HasMaxLength(100);
        builder.Property(e => e.SoilLevel).HasMaxLength(100);
        builder.Property(e => e.FloodRisk).HasMaxLength(50);
        builder.Property(e => e.LandUseZoning).HasMaxLength(100);
        builder.Property(e => e.LandUseZoningOther).HasMaxLength(200);

        // =====================================================
        // LAND SECTION - Road Access
        // =====================================================
        builder.Property(e => e.AccessRoadType).HasMaxLength(50);
        builder.Property(e => e.AccessRoadWidth).HasPrecision(10, 2);
        builder.Property(e => e.RightOfWay).HasMaxLength(200);
        builder.Property(e => e.RoadFrontage).HasPrecision(10, 2);
        builder.Property(e => e.RoadPassInFrontOfLand).HasMaxLength(200);
        builder.Property(e => e.LandAccessibility).HasMaxLength(100);
        builder.Property(e => e.LandAccessibilityDescription).HasMaxLength(500);
        builder.Property(e => e.RoadSurfaceType).HasMaxLength(50);
        builder.Property(e => e.RoadSurfaceTypeOther).HasMaxLength(200);

        // =====================================================
        // LAND SECTION - Utilities & Infrastructure
        // =====================================================
        builder.Property(e => e.ElectricityDistance).HasPrecision(10, 2);
        builder.Property(e => e.PublicUtilities).HasMaxLength(500);
        builder.Property(e => e.PublicUtilitiesOther).HasMaxLength(200);
        builder.Property(e => e.LandEntranceExit).HasMaxLength(100);
        builder.Property(e => e.LandEntranceExitOther).HasMaxLength(200);
        builder.Property(e => e.TransportationAccess).HasMaxLength(200);
        builder.Property(e => e.TransportationAccessOther).HasMaxLength(200);
        builder.Property(e => e.PropertyAnticipation).HasMaxLength(500);

        // =====================================================
        // LAND SECTION - Legal Restrictions
        // =====================================================
        builder.Property(e => e.ExpropriationRemark).HasMaxLength(500);
        builder.Property(e => e.ExpropriationLineRemark).HasMaxLength(500);
        builder.Property(e => e.RoyalDecree).HasMaxLength(500);
        builder.Property(e => e.EncroachmentRemark).HasMaxLength(500);
        builder.Property(e => e.EncroachmentArea).HasPrecision(18, 4);
        builder.Property(e => e.LandlockedRemark).HasMaxLength(500);
        builder.Property(e => e.ForestBoundaryRemark).HasMaxLength(500);
        builder.Property(e => e.OtherLegalLimitations).HasMaxLength(1000);
        builder.Property(e => e.EvictionStatus).HasMaxLength(100);
        builder.Property(e => e.EvictionStatusOther).HasMaxLength(200);
        builder.Property(e => e.AllocationStatus).HasMaxLength(100);

        // =====================================================
        // LAND SECTION - Adjacent Boundaries (N/S/E/W)
        // =====================================================
        builder.Property(e => e.NorthAdjacentArea).HasMaxLength(200);
        builder.Property(e => e.NorthBoundaryLength).HasPrecision(10, 2);
        builder.Property(e => e.SouthAdjacentArea).HasMaxLength(200);
        builder.Property(e => e.SouthBoundaryLength).HasPrecision(10, 2);
        builder.Property(e => e.EastAdjacentArea).HasMaxLength(200);
        builder.Property(e => e.EastBoundaryLength).HasPrecision(10, 2);
        builder.Property(e => e.WestAdjacentArea).HasMaxLength(200);
        builder.Property(e => e.WestBoundaryLength).HasPrecision(10, 2);

        // =====================================================
        // LAND SECTION - Other Land Features
        // =====================================================
        builder.Property(e => e.PondArea).HasPrecision(18, 4);
        builder.Property(e => e.PondDepth).HasPrecision(10, 2);

        // =====================================================
        // BUILDING SECTION - Identification
        // =====================================================
        builder.Property(e => e.BuildingNumber).HasMaxLength(50);
        builder.Property(e => e.ModelName).HasMaxLength(100);
        builder.Property(e => e.BuiltOnTitleNumber).HasMaxLength(100);
        builder.Property(e => e.HouseNumber).HasMaxLength(50);

        // =====================================================
        // BUILDING SECTION - Building Info
        // =====================================================
        builder.Property(e => e.BuildingType).HasMaxLength(50);
        builder.Property(e => e.BuildingTypeOther).HasMaxLength(200);
        builder.Property(e => e.IsResidentialRemark).HasMaxLength(200);

        // =====================================================
        // BUILDING SECTION - Building Status
        // =====================================================
        builder.Property(e => e.BuildingCondition).HasMaxLength(50);
        builder.Property(e => e.ConstructionCompletionPercent).HasPrecision(5, 2);
        builder.Property(e => e.MaintenanceStatus).HasMaxLength(50);
        builder.Property(e => e.RenovationHistory).HasMaxLength(1000);

        // =====================================================
        // BUILDING SECTION - Building Area
        // =====================================================
        builder.Property(e => e.TotalBuildingArea).HasPrecision(18, 2);
        builder.Property(e => e.BuildingAreaUnit).HasMaxLength(20);
        builder.Property(e => e.UsableArea).HasPrecision(18, 2);

        // =====================================================
        // BUILDING SECTION - Construction Style
        // =====================================================
        builder.Property(e => e.BuildingMaterial).HasMaxLength(100);
        builder.Property(e => e.BuildingStyle).HasMaxLength(100);
        builder.Property(e => e.ConstructionStyleType).HasMaxLength(100);
        builder.Property(e => e.ConstructionStyleRemark).HasMaxLength(500);
        builder.Property(e => e.ConstructionType).HasMaxLength(100);
        builder.Property(e => e.ConstructionTypeOther).HasMaxLength(200);

        // =====================================================
        // BUILDING SECTION - Structure Components
        // =====================================================
        builder.Property(e => e.StructureType).HasMaxLength(50);
        builder.Property(e => e.StructureTypeOther).HasMaxLength(200);
        builder.Property(e => e.FoundationType).HasMaxLength(50);
        builder.Property(e => e.RoofFrameType).HasMaxLength(50);
        builder.Property(e => e.RoofFrameTypeOther).HasMaxLength(200);
        builder.Property(e => e.RoofType).HasMaxLength(50);
        builder.Property(e => e.RoofTypeOther).HasMaxLength(200);
        builder.Property(e => e.RoofMaterial).HasMaxLength(100);
        builder.Property(e => e.CeilingType).HasMaxLength(50);
        builder.Property(e => e.CeilingTypeOther).HasMaxLength(200);
        builder.Property(e => e.InteriorWallType).HasMaxLength(50);
        builder.Property(e => e.InteriorWallTypeOther).HasMaxLength(200);
        builder.Property(e => e.ExteriorWallType).HasMaxLength(50);
        builder.Property(e => e.ExteriorWallTypeOther).HasMaxLength(200);
        builder.Property(e => e.WallMaterial).HasMaxLength(100);
        builder.Property(e => e.FloorMaterial).HasMaxLength(100);
        builder.Property(e => e.FenceType).HasMaxLength(50);
        builder.Property(e => e.FenceTypeOther).HasMaxLength(200);

        // =====================================================
        // BUILDING SECTION - Decoration
        // =====================================================
        builder.Property(e => e.DecorationType).HasMaxLength(100);
        builder.Property(e => e.DecorationTypeOther).HasMaxLength(200);

        // =====================================================
        // BUILDING SECTION - Utilization
        // =====================================================
        builder.Property(e => e.UtilizationType).HasMaxLength(100);
        builder.Property(e => e.OtherPurposeUsage).HasMaxLength(500);

        // =====================================================
        // BUILDING SECTION - Permits
        // =====================================================
        builder.Property(e => e.BuildingPermitNumber).HasMaxLength(100);

        // =====================================================
        // BUILDING SECTION - Pricing
        // =====================================================
        builder.Property(e => e.BuildingInsurancePrice).HasPrecision(18, 2);
        builder.Property(e => e.SellingPrice).HasPrecision(18, 2);
        builder.Property(e => e.ForcedSalePrice).HasPrecision(18, 2);

        // =====================================================
        // SHARED - Remarks
        // =====================================================
        builder.Property(e => e.LandRemark).HasMaxLength(2000);
        builder.Property(e => e.BuildingRemark).HasMaxLength(2000);
    }
}
