namespace Appraisal.Application.Features.Appraisals.CreateCondoProperty;

/// <summary>
/// Request to create a condo property with its appraisal detail
/// </summary>
public record CreateCondoPropertyRequest(
    // Required
    string OwnerName,
    // Property Identification
    string? PropertyName = null,
    string? CondoName = null,
    string? BuildingNumber = null,
    string? ModelName = null,
    string? BuiltOnTitleNo = null,
    string? CondoRegistrationNo = null,
    string? RoomNo = null,
    int? FloorNo = null,
    decimal? UsableArea = null,
    string? Description = null,
    // Coordinates
    decimal? Latitude = null,
    decimal? Longitude = null,
    // Address
    string? SubDistrict = null,
    string? District = null,
    string? Province = null,
    string? LandOffice = null,
    // Owner Details
    bool? IsOwnerVerified = null,
    string? BuildingConditionType = null,
    bool? HasObligation = null,
    string? ObligationDetails = null,
    bool? IsDocumentValidated = null,
    // Location Details
    string? LocationType = null,
    string? Street = null,
    string? Soi = null,
    decimal? DistanceFromMainRoad = null,
    decimal? AccessRoadWidth = null,
    short? RightOfWay = null,
    string? RoadSurfaceType = null,
    string? RoadSurfaceTypeOther = null,
    List<string>? PublicUtility = null,
    string? PublicUtilityOther = null,
    // Building Info
    string? DecorationType = null,
    string? DecorationTypeOther = null,
    int? BuildingAge = null,
    int? ConstructionYear = null,
    int? NumberOfFloors = null,
    string? BuildingForm = null,
    string? ConstructionMaterialType = null,
    // Layout & Materials
    string? RoomLayoutType = null,
    string? RoomLayoutTypeOther = null,
    List<string>? LocationView = null,
    string? GroundFloorMaterial = null,
    string? GroundFloorMaterialOther = null,
    string? UpperFloorMaterial = null,
    string? UpperFloorMaterialOther = null,
    string? BathroomFloorMaterial = null,
    string? BathroomFloorMaterialOther = null,
    string? RoofType = null,
    string? RoofTypeOther = null,
    // Area
    decimal? TotalBuildingArea = null,
    // Legal Restrictions
    bool? IsExpropriated = null,
    string? ExpropriationRemark = null,
    bool? IsInExpropriationLine = null,
    string? ExpropriationLineRemark = null,
    string? RoyalDecree = null,
    bool? IsForestBoundary = null,
    string? ForestBoundaryRemark = null,
    // Facilities & Environment
    List<string>? FacilityType = null,
    string? FacilityTypeOther = null,
    List<string>? EnvironmentType = null,
    // Pricing
    decimal? BuildingInsurancePrice = null,
    decimal? SellingPrice = null,
    decimal? ForcedSalePrice = null,
    // Other
    string? Remark = null
);
