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
    string? BuildingNo,
    string? ModelName,
    string? BuiltOnTitleNo,
    string? CondoRegisNo,
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
    string Owner,
    bool VerifiableOwner,
    string? CondoCondition,
    bool IsObligation,
    string? Obligation,
    bool DocValidate,
    // Location Details
    string? CondoLocation,
    string? Street,
    string? Soi,
    decimal? Distance,
    decimal? RoadWidth,
    string? RightOfWay,
    string? RoadSurface,
    string? PublicUtility,
    string? PublicUtilityOther,
    // Building Info
    string? Decoration,
    string? DecorationOther,
    int? BuildingYear,
    int? NumberOfFloors,
    string? BuildingForm,
    string? ConstMaterial,
    // Layout & Materials
    string? RoomLayout,
    string? RoomLayoutOther,
    string? LocationView,
    string? GroundFloorMaterial,
    string? GroundFloorMaterialOther,
    string? UpperFloorMaterial,
    string? UpperFloorMaterialOther,
    string? BathroomFloorMaterial,
    string? BathroomFloorMaterialOther,
    string? Roof,
    string? RoofOther,
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
    string? CondoFacility,
    string? CondoFacilityOther,
    string? Environment,
    // Pricing
    decimal? BuildingInsurancePrice,
    decimal? SellingPrice,
    decimal? ForceSellingPrice,
    // Other
    string? Remark
);
