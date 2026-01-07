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
    string? CondoRegisNo = null,
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
    string? BuildingCondition = null,
    bool? HasObligation = null,
    string? ObligationDetails = null,
    bool? DocValidate = null,
    // Location Details
    string? CondoLocation = null,
    string? Street = null,
    string? Soi = null,
    decimal? DistanceFromMainRoad = null,
    decimal? AccessRoadWidth = null,
    string? RightOfWay = null,
    string? RoadSurfaceType = null,
    string? PublicUtility = null,
    string? PublicUtilityOther = null,
    // Building Info
    string? Decoration = null,
    string? DecorationOther = null,
    int? BuildingYear = null,
    int? NumberOfFloors = null,
    string? BuildingForm = null,
    string? ConstMaterial = null,
    // Layout & Materials
    string? RoomLayout = null,
    string? RoomLayoutOther = null,
    string? LocationView = null,
    string? GroundFloorMaterial = null,
    string? GroundFloorMaterialOther = null,
    string? UpperFloorMaterial = null,
    string? UpperFloorMaterialOther = null,
    string? BathroomFloorMaterial = null,
    string? BathroomFloorMaterialOther = null,
    string? Roof = null,
    string? RoofOther = null,
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
    string? CondoFacility = null,
    string? CondoFacilityOther = null,
    string? Environment = null,
    // Pricing
    decimal? BuildingInsurancePrice = null,
    decimal? SellingPrice = null,
    decimal? ForcedSalePrice = null,
    // Other
    string? Remark = null
);
