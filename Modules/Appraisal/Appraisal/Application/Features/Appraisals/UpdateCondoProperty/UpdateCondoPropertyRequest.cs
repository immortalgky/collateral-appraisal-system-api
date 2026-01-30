namespace Appraisal.Application.Features.Appraisals.UpdateCondoProperty;

/// <summary>
/// Request to update a condo property detail
/// </summary>
public record UpdateCondoPropertyRequest(
    // Property Identification
    string? PropertyName = null,
    string? CondoName = null,
    string? BuildingNumber = null,
    string? ModelName = null,
    string? BuiltOnTitleNumber = null,
    string? CondoRegistrationNumber = null,
    string? RoomNumber = null,
    int? FloorNumber = null,
    decimal? UsableArea = null,
    // Coordinates
    decimal? Latitude = null,
    decimal? Longitude = null,
    // Address
    string? SubDistrict = null,
    string? District = null,
    string? Province = null,
    string? LandOffice = null,
    // Owner
    string? OwnerName = null,
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
    List<string>? PublicUtilityType = null,
    string? PublicUtilityTypeOther = null,
    // Building Info
    string? DecorationType = null,
    string? DecorationTypeOther = null,
    int? BuildingAge = null,
    int? ConstructionYear = null,
    decimal? NumberOfFloors = null,
    string? BuildingFormType = null,
    string? ConstructionMaterialType = null,
    // Layout & Materials
    string? RoomLayoutType = null,
    string? RoomLayoutTypeOther = null,
    List<string>? LocationViewType = null,
    string? GroundFloorMaterialType = null,
    string? GroundFloorMaterialTypeOther = null,
    string? UpperFloorMaterialType = null,
    string? UpperFloorMaterialTypeOther = null,
    string? BathroomFloorMaterialType = null,
    string? BathroomFloorMaterialTypeOther = null,
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
