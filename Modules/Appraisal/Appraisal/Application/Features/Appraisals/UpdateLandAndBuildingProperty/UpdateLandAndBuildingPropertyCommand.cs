using Appraisal.Application.Features.Appraisals.CreateLandProperty;

namespace Appraisal.Application.Features.Appraisals.UpdateLandAndBuildingProperty;

/// <summary>
/// Command to update a land and building property detail
/// </summary>
public record UpdateLandAndBuildingPropertyCommand(
    Guid AppraisalId,
    Guid PropertyId,
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
    // Land Titles
    List<LandTitleItemData>? Titles = null,
    //=================================
    // Building - Identification
    string? BuildingNumber = null,
    string? ModelName = null,
    string? BuiltOnTitleNumber = null,
    string? HouseNumber = null,
    // Building Status
    string? BuildingConditionType = null,
    bool? IsUnderConstruction = null,
    decimal? ConstructionCompletionPercent = null,
    DateTime? ConstructionLicenseExpirationDate = null,
    bool? IsAppraisable = null,
    // Building Info
    string? BuildingType = null,
    string? BuildingTypeOther = null,
    int? NumberOfFloors = null,
    string? DecorationType = null,
    string? DecorationTypeOther = null,
    bool? IsEncroachingOthers = null,
    string? EncroachingOthersRemark = null,
    decimal? EncroachingOthersArea = null,
    // Construction Details
    string? BuildingMaterialType = null,
    string? BuildingStyleType = null,
    bool? IsResidential = null,
    int? BuildingAge = null,
    int? ConstructionYear = null,
    string? ResidentialRemark = null,
    string? ConstructionStyleType = null,
    string? ConstructionStyleRemark = null,
    // Structure Components
    List<string>? StructureType = null,
    string? StructureTypeOther = null,
    List<string>? RoofFrameType = null,
    string? RoofFrameTypeOther = null,
    List<string>? RoofType = null,
    string? RoofTypeOther = null,
    List<string>? CeilingType = null,
    string? CeilingTypeOther = null,
    List<string>? InteriorWallType = null,
    string? InteriorWallTypeOther = null,
    List<string>? ExteriorWallType = null,
    string? ExteriorWallTypeOther = null,
    List<string>? FenceType = null,
    string? FenceTypeOther = null,
    string? ConstructionType = null,
    string? ConstructionTypeOther = null,
    // Utilization
    string? UtilizationType = null,
    string? UtilizationTypeOther = null,
    // Area & Pricing
    decimal? TotalBuildingArea = null,
    decimal? BuildingInsurancePrice = null,
    decimal? SellingPrice = null,
    decimal? ForcedSalePrice = null,
    // Remarks
    string? LandRemark = null,
    string? BuildingRemark = null,
    // Depreciation Details (null = no-op, list = sync)
    List<DepreciationItemData>? DepreciationDetails = null,
    // Surfaces (null = no-op, list = sync)
    List<SurfaceItemData>? Surfaces = null
) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;

public record DepreciationItemData(
    Guid? Id,
    string DepreciationMethod,
    string? AreaDescription = null,
    decimal Area = 0,
    short Year = 0,
    bool IsBuilding = true,
    decimal PricePerSqMBeforeDepreciation = 0,
    decimal PriceBeforeDepreciation = 0,
    decimal PricePerSqMAfterDepreciation = 0,
    decimal PriceAfterDepreciation = 0,
    decimal DepreciationYearPct = 0,
    decimal TotalDepreciationPct = 0,
    decimal PriceDepreciation = 0,
    List<DepreciationPeriodItemData>? DepreciationPeriods = null
);

public record DepreciationPeriodItemData(
    int AtYear,
    int ToYear,
    decimal DepreciationPerYear,
    decimal TotalDepreciationPct,
    decimal PriceDepreciation
);

public record SurfaceItemData(
    Guid? Id,
    int FromFloorNumber,
    int ToFloorNumber,
    string? FloorType = null,
    string? FloorStructureType = null,
    string? FloorStructureTypeOther = null,
    string? FloorSurfaceType = null,
    string? FloorSurfaceTypeOther = null
);