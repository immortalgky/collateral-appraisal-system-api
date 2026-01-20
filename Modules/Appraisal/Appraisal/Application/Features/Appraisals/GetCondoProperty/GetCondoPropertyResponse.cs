namespace Appraisal.Application.Features.Appraisals.GetCondoProperty;

/// <summary>
/// Response for getting a condo property
/// </summary>
public record GetCondoPropertyResponse(
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
    string? BuiltOnTitleNumber,
    string? CondoRegistrationNumber,
    string? RoomNumber,
    int? FloorNumber,
    decimal? UsableArea,
    // Coordinates
    decimal? Latitude,
    decimal? Longitude,
    // Address
    string? SubDistrict,
    string? District,
    string? Province,
    string? LandOffice,
    // Owner
    string? OwnerName,
    bool? IsOwnerVerified,
    string? BuildingConditionType,
    bool? HasObligation,
    string? ObligationDetails,
    bool? IsDocumentValidated,
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
    string? GroundFloorMaterialType,
    string? GroundFloorMaterialTypeOther,
    string? UpperFloorMaterialType,
    string? UpperFloorMaterialTypeOther,
    string? BathroomFloorMaterialType,
    string? BathroomFloorMaterialTypeOther,
    string? RoofType,
    string? RoofTypeOther,
    // Area
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
    // Pricing
    decimal? BuildingInsurancePrice,
    decimal? SellingPrice,
    decimal? ForceSellingPrice,
    // Other
    string? Remark
);
