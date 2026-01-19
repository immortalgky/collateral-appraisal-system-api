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
    string? BuildingNo,
    string? ModelName,
    string? BuiltOnTitleNo,
    string? CondoRegistrationNo,
    string? RoomNo,
    int? FloorNo,
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
    string OwnerName,
    bool IsOwnerVerified,
    string? BuildingConditionType,
    bool IsObligation,
    string? Obligation,
    bool IsDocumentValidated,
    // Location Details
    string? LocationType,
    string? Street,
    string? Soi,
    decimal? Distance,
    decimal? RoadWidth,
    string? RightOfWay,
    string? RoadSurface,
    List<string>? PublicUtility,
    string? PublicUtilityOther,
    // Building Info
    string? DecorationType,
    string? DecorationTypeOther,
    int? ConstructionYear,
    int? NumberOfFloors,
    string? BuildingForm,
    string? ConstructionMaterialType,
    // Layout & Materials
    string? RoomLayoutType,
    string? RoomLayoutTypeOther,
    List<string>? LocationView,
    string? GroundFloorMaterial,
    string? GroundFloorMaterialOther,
    string? UpperFloorMaterial,
    string? UpperFloorMaterialOther,
    string? BathroomFloorMaterial,
    string? BathroomFloorMaterialOther,
    string? RoofType,
    string? RoofTypeOther,
    // Area
    decimal? TotalAreaInSqM,
    // Legal Restrictions
    bool IsExpropriate,
    string? IsExpropriateRemark,
    bool InLineExpropriate,
    string? InLineExpropriateRemark,
    string? RoyalDecree,
    bool IsForestBoundary,
    string? IsForestBoundaryRemark,
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
