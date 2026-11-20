using Appraisal.Application.Features.Appraisals.CreateLandProperty;
using Appraisal.Application.Features.Appraisals.Shared;

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
    string? DopaSubDistrict = null,
    string? DopaDistrict = null,
    string? DopaProvince = null,
    // Owner Details
    string? OwnerNameLand = null,
    bool? IsOwnerVerifiedLand = null,
    List<string>? ObligationType = null,
    string? ObligationDetails = null,
    string? MortgageDetails = null,
    // Document Verification
    bool? IsLandLocationVerified = null,
    List<string>? LandCheckMethodType = null,
    string? LandCheckMethodTypeOther = null,
    string? LandLocationRemark = null,
    // Location Details
    string? Street = null,
    string? Soi = null,
    decimal? DistanceFromMainRoad = null,
    string? Village = null,
    string? AddressLocation = null,
    // Land Characteristics
    string? LandShapeType = null,
    string? LandShapeTypeOther = null,
    string? DevelopmentSuitabilityType = null,
    string? UrbanPlanningType = null,
    string? UrbanPlanningColor = null,
    string? UrbanPlanningColorOther = null,
    List<string>? LandZoneType = null,
    string? LandZoneTypeOther = null,
    List<string>? PlotLocationType = null,
    string? PlotLocationTypeOther = null,
    string? LandFillType = null,
    string? LandFillTypeOther = null,
    decimal? LandFillPercent = null,
    decimal? SoilLevel = null,
    // Road Access
    decimal? CarriagewayWidth = null,
    decimal? RightOfWayWidth = null,
    short? NumberOfLanes = null,
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
    List<string>? EnvironmentType = null,
    string? EnvironmentTypeOther = null,
    string? PropertyAnticipationType = null,
    string? PropertyAnticipationTypeOther = null,
    // Legal Restrictions
    bool? IsExpropriated = null,
    string? ExpropriationRemark = null,
    bool? IsInExpropriationLine = null,
    string? ExpropriationLineRemark = null,
    string? RoyalDecree = null,
    bool? IsEncroached = null,
    string? EncroachmentRemark = null,
    bool? IsLandlocked = null,
    string? LandlockedRemark = null,
    bool? IsForestBoundary = null,
    string? ForestBoundaryRemark = null,
    bool? HasFloodProblem = null,
    string? FloodProblemRemark = null,
    bool? HasPollutionProblem = null,
    List<string>? PollutionType = null,
    string? PollutionTypeOther = null,
    string? PollutionProblemRemark = null,
    bool? HasOtherEnvironmentProblem = null,
    string? OtherEnvironmentProblemRemark = null,
    string? OtherLegalLimitations = null,
    List<string>? EvictionType = null,
    string? EvictionTypeOther = null,
    string? EvictionRemark = null,
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
    bool? HasBuilding = null,
    string? HasBuildingOther = null,
    // Land Titles
    List<LandTitleItemData>? Titles = null,
    // Area deductions (null = no-op, empty list = clear all)
    List<LandAreaDeductionData>? LandAreaDeductions = null,
    //=================================
    // Building - Identification
    string? BuildingNumber = null,
    string? ModelName = null,
    string? BuiltOnTitleNumber = null,
    string? HouseNumber = null,
    string? HouseNumberStatus = null,
    // Owner Details
    string? OwnerNameBuilding = null,
    bool? IsOwnerVerifiedBuilding = null,
    // Building Status
    string? BuildingConditionType = null,
    string? BuildingConditionTypeOther = null,
    bool? IsUnderConstruction = null,
    DateTime? ConstructionLicenseExpirationDate = null,
    string? ConstructionLicenseNumber = null,
    bool? IsAppraisable = null,
    string? NotAppraisableReason = null,
    // Building Info
    string? BuildingType = null,
    string? BuildingTypeOther = null,
    decimal? NumberOfFloors = null,
    string? DecorationType = null,
    string? DecorationTypeOther = null,
    bool? IsEncroachingOthers = null,
    string? EncroachingOthersRemark = null,
    decimal? EncroachingOthersArea = null,
    // Construction Details
    string? BuildingMaterialType = null,
    string? BuildingStyleType = null,
    string? BuildingStyleTypeOther = null,
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
    // Building detail (appraisal book)
    decimal? BuildingWidth = null,
    decimal? BuildingLength = null,
    string? SpaceDivision = null,
    string? MaintenanceType = null,
    string? MaintenanceTypeOther = null,
    List<string>? DoorType = null,
    string? DoorTypeOther = null,
    List<string>? WindowType = null,
    string? WindowTypeOther = null,
    List<string>? SanitaryType = null,
    string? SanitaryTypeOther = null,
    List<string>? BuildingSystemType = null,
    string? BuildingSystemTypeOther = null,
    // Utilization
    string? UtilizationType = null,
    string? UtilizationTypeOther = null,
    // Area & Pricing
    decimal? TotalBuildingArea = null,
    decimal? BuildingCostValue = null,
    decimal? BuildingInsurancePrice = null,
    // Remarks
    string? Remark = null,
    // Depreciation Details (null = no-op, list = sync)
    List<DepreciationItemData>? DepreciationDetails = null,
    // Surfaces (null = no-op, list = sync)
    List<SurfaceItemData>? Surfaces = null,
    // Construction Inspection (null = no-op)
    ConstructionInspectionData? ConstructionInspection = null,
    // Rental
    bool? IsRentedOut = null,
    LeaseAgreementData? LeaseAgreement = null,
    RentalInfoData? RentalInfo = null
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
