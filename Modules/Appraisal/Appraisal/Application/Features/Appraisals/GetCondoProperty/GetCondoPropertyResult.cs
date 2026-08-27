namespace Appraisal.Application.Features.Appraisals.GetCondoProperty;

/// <summary>
/// Result of getting a condo property
/// </summary>
public record GetCondoPropertyResult(
    // Property
    Guid PropertyId,
    Guid AppraisalId,
    int SequenceNumber,
    string PropertyType,
    string? Description,
    // Detail
    Guid DetailId,
    // Property Identification
    string? PropertyName,
    string? CondoName,
    string? BuildingNumber,
    string? ModelName,
    string? TitleNumber,
    string? CondoRegistrationNumber,
    string? RoomNumber,
    string? FloorNumber,
    // Surveyed floor and the unit deed's type. Exposed so the admin data-correction screen can show
    // the stored value before someone corrects it — they were writable in the domain but invisible
    // in every read model. (TitleNumber is already declared above.)
    int? PhysicalFloorNumber,
    string? TitleType,
    decimal? UsableArea,
    bool? IsUnderConstruction,
    // Coordinates
    decimal? Latitude,
    decimal? Longitude,
    // Address
    string? SubDistrict,
    string? District,
    string? Province,
    string? LandOffice,
    string? DopaSubDistrict,
    string? DopaDistrict,
    string? DopaProvince,
    // Owner
    string? OwnerName,
    bool? IsOwnerVerified,
    string? BuildingConditionType,
    string? BuildingConditionTypeOther,
    string? HasObligation,
    string? ObligationDetails,
    string? DocumentValidationResultType,
    // Location Details
    string? LocationType,
    string? Street,
    string? Soi,
    decimal? DistanceFromMainRoad,
    decimal? AccessRoadWidth,
    short? RightOfWay,
    string? RoadSurfaceType,
    string? RoadSurfaceTypeOther,
    List<string>? PublicUtilityType,
    string? PublicUtilityTypeOther,
    List<string>? LandEntranceExitType,
    string? LandEntranceExitTypeOther,
    // Land Characteristics
    string? LandFillType,
    string? LandFillTypeOther,
    string? UrbanPlanningType,
    List<string>? LandUseType,
    string? LandUseTypeOther,
    // Building Info
    string? DecorationType,
    string? DecorationTypeOther,
    int? BuildingAge,
    decimal? NumberOfFloors,
    string? BuildingFormType,
    string? ConstructionMaterialType,
    // Layout & Materials
    string? RoomLayoutType,
    string? RoomLayoutTypeOther,
    List<string>? LocationViewType,
    string? LocationViewTypeOther,
    string? GroundFloorMaterialType,
    string? GroundFloorMaterialTypeOther,
    string? UpperFloorMaterialType,
    string? UpperFloorMaterialTypeOther,
    string? BathroomFloorMaterialType,
    string? BathroomFloorMaterialTypeOther,
    List<string>? RoofType,
    string? RoofTypeOther,
    // Area
    IReadOnlyList<CondoAppraisalAreaDetailDto>? AreaDetails,
    decimal? TotalBuildingArea,
    // Legal Restrictions
    bool? IsExpropriated,
    string? ExpropriationRemark,
    bool? IsInExpropriationLine,
    string? ExpropriationLineRemark,
    string? RoyalDecree,
    bool? IsForestBoundary,
    string? ForestBoundaryRemark,
    // Facilities & Environment
    List<string>? FacilityType,
    string? FacilityTypeOther,
    List<string>? EnvironmentType,
    string? EnvironmentTypeOther,
    // Pricing
    bool? IsMissingFromSurvey,
    decimal? GovernmentPricePerSqm,
    decimal? GovernmentPrice,
    string? FireInsuranceCondition,
    decimal? BuildingInsurancePrice,
    decimal? SellingPrice,
    decimal? ForceSellingPrice,
    // Other
    string? Remark,
    // Construction Inspection
    ConstructionInspectionDto? ConstructionInspection
);