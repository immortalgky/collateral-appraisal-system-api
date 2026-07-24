using Appraisal.Application.Features.Appraisals.Shared;

namespace Appraisal.Application.Features.Appraisals.GetLeaseAgreementCondoProperty;

/// <summary>
/// Result of getting a lease agreement condo property
/// </summary>
public record GetLeaseAgreementCondoPropertyResult(
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
    string? FloorNumber,
    decimal? UsableArea,
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
    decimal? BuildingInsurancePrice,
    decimal? SellingPrice,
    decimal? ForceSellingPrice,
    // Other
    string? Remark,
    // Lease Agreement & Rental Info
    LeaseAgreementDetailDto? LeaseAgreement,
    RentalInfoDto? RentalInfo
);
