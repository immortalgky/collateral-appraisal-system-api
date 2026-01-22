namespace Appraisal.Application.Features.Appraisals.CreateLandAndBuildingProperty;

/// <summary>
/// Request to create a land and building property with its appraisal detail
/// </summary>
public record CreateLandAndBuildingPropertyRequest(
    // Property Identification
    string? PropertyName = null,
    string? LandDescription = null,
    // Coordinates
    decimal? Latitude = null,
    decimal? Longitude = null,
    // Address
    string? SubDistrict = null,
    string? District = null,
    string? Province = null,
    string? LandOffice = null,
    // Owner Details
    string? OwnerName = null,
    bool? IsOwnerVerified = null,
    bool? HasObligation = null,
    string? ObligationDetails = null,
    // Document Verification
    bool? IsLandLocationVerified = null,
    string? LandCheckMethodType = null,
    string? LandCheckMethodTypeOther = null,
    // Location Details
    string? Street = null,
    string? Soi = null,
    decimal? DistanceFromMainRoad = null,
    string? Village = null,
    string? AddressLocation = null,
    // Land Characteristics
    string? LandShapeType = null,
    string? UrbanPlanningType = null,
    List<string>? LandZoneType = null,
    List<string>? PlotLocationType = null,
    string? PlotLocationTypeOther = null,
    string? LandFillType = null,
    string? LandFillTypeOther = null,
    decimal? LandFillPercent = null,
    decimal? SoilLevel = null,
    // Road Access
    decimal? AccessRoadWidth = null,
    short? RightOfWay = null,
    decimal? RoadFrontage = null,
    int? NumberOfSidesFacingRoad = null,
    string? RoadPassInFrontOfLand = null,
    string? LandAccessibilityType = null,
    string? LandAccessibilityRemark = null,
    string? RoadSurfaceType = null,
    string? RoadSurfaceTypeOther = null,
    // Utilities & Infrastructure
    bool? HasElectricity = null,
    decimal? ElectricityDistance = null,
    List<string>? PublicUtilityType = null,
    string? PublicUtilityTypeOther = null,
    List<string>? LandUseType = null,
    string? LandUseTypeOther = null,
    List<string>? LandEntranceExitType = null,
    string? LandEntranceExitTypeOther = null,
    List<string>? TransportationAccessType = null,
    string? TransportationAccessTypeOther = null,
    string? PropertyAnticipationType = null,
    // Legal Restrictions
    bool? IsExpropriated = null,
    string? ExpropriationRemark = null,
    bool? IsInExpropriationLine = null,
    string? ExpropriationLineRemark = null,
    string? RoyalDecree = null,
    bool? IsEncroached = null,
    string? EncroachmentRemark = null,
    decimal? EncroachmentArea = null,
    bool? IsLandlocked = null,
    string? LandlockedRemark = null,
    bool? IsForestBoundary = null,
    string? ForestBoundaryRemark = null,
    string? OtherLegalLimitations = null,
    List<string>? EvictionType = null,
    string? EvictionTypeOther = null,
    string? AllocationType = null,
    // Adjacent Boundaries
    string? NorthAdjacentArea = null,
    decimal? NorthBoundaryLength = null,
    string? SouthAdjacentArea = null,
    decimal? SouthBoundaryLength = null,
    string? EastAdjacentArea = null,
    decimal? EastBoundaryLength = null,
    string? WestAdjacentArea = null,
    decimal? WestBoundaryLength = null,
    // Other Features
    decimal? PondArea = null,
    decimal? PondDepth = null,
    //=================================
    // Building - Identification
    string? BuildingNumber = null,
    string? ModelName = null,
    string? BuiltOnTitleNumber = null,
    string? HouseNumber = null,
    // Building - Info
    string? BuildingType = null,
    string? BuildingTypeOther = null,
    int? NumberOfBuildings = null,
    int? BuildingAge = null,
    int? ConstructionYear = null,
    string? ResidentialRemark = null,
    // Building - Status
    string? BuildingCondition = null,
    bool? IsUnderConstruction = null,
    decimal? ConstructionCompletionPercent = null,
    DateTime? ConstructionLicenseExpirationDate = null,
    bool? IsAppraisable = null,
    string? MaintenanceStatus = null,
    string? RenovationHistory = null,
    // Building - Area
    decimal? TotalBuildingArea = null,
    string? BuildingAreaUnit = null,
    decimal? UsableArea = null,
    // Building - Structure
    int? NumberOfFloors = null,
    // Building - Style
    string? BuildingMaterial = null,
    string? BuildingStyle = null,
    bool? IsResidential = null,
    string? ConstructionStyleType = null,
    string? ConstructionStyleRemark = null,
    string? ConstructionType = null,
    string? ConstructionTypeOther = null,
    // Building - Components
    List<string>? StructureType = null,
    string? StructureTypeOther = null,
    string? FoundationType = null,
    List<string>? RoofFrameType = null,
    string? RoofFrameTypeOther = null,
    List<string>? RoofType = null,
    string? RoofTypeOther = null,
    string? RoofMaterial = null,
    List<string>? CeilingType = null,
    string? CeilingTypeOther = null,
    List<string>? InteriorWallType = null,
    string? InteriorWallTypeOther = null,
    List<string>? ExteriorWallType = null,
    string? ExteriorWallTypeOther = null,
    string? WallMaterial = null,
    string? FloorMaterial = null,
    List<string>? FenceType = null,
    string? FenceTypeOther = null,
    // Building - Decoration
    string? DecorationType = null,
    string? DecorationTypeOther = null,
    // Building - Utilization
    string? UtilizationType = null,
    string? OtherPurposeUsage = null,
    // Building - Permits
    string? BuildingPermitNumber = null,
    DateTime? BuildingPermitDate = null,
    bool? HasOccupancyPermit = null,
    // Building - Pricing
    decimal? BuildingInsurancePrice = null,
    decimal? SellingPrice = null,
    decimal? ForcedSalePrice = null,
    // Remarks
    string? LandRemark = null,
    string? BuildingRemark = null
);
