using Appraisal.Application.Features.Appraisals.UpdateLandAndBuildingProperty;

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
    string? CondoRegistrationNumber = null,
    string? RoomNumber = null,
    string? FloorNumber = null,
    decimal? UsableArea = null,
    bool? IsUnderConstruction = null,
    // Unit deed identifiers (collateral dedup key)
    string? TitleNumber = null,
    string? TitleType = null,
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
    // Owner
    string? OwnerName = null,
    bool? IsOwnerVerified = null,
    string? BuildingConditionType = null,
    string? BuildingConditionTypeOther = null,
    List<string>? ObligationType = null,
    string? ObligationDetails = null,
    string? MortgageDetails = null,
    string? DocumentValidationResultType = null,
    // Location Details
    string? LocationType = null,
    string? LocationRemark = null,
    string? Street = null,
    string? Soi = null,
    decimal? DistanceFromMainRoad = null,
    decimal? CarriagewayWidth = null,
    decimal? RightOfWayWidth = null,
    short? NumberOfLanes = null,
    string? RoadSurfaceType = null,
    string? RoadSurfaceTypeOther = null,
    List<string>? PublicUtilityType = null,
    string? PublicUtilityTypeOther = null,
    List<string>? LandEntranceExitType = null,
    string? LandEntranceExitTypeOther = null,
    // Land Characteristics
    string? LandFillType = null,
    string? LandFillTypeOther = null,
    string? UrbanPlanningType = null,
    string? UrbanPlanningColor = null,
    string? UrbanPlanningColorOther = null,
    List<string>? LandUseType = null,
    string? LandUseTypeOther = null,
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
    string? LocationViewTypeOther = null,
    string? GroundFloorMaterialType = null,
    string? GroundFloorMaterialTypeOther = null,
    string? UpperFloorMaterialType = null,
    string? UpperFloorMaterialTypeOther = null,
    string? BathroomFloorMaterialType = null,
    string? BathroomFloorMaterialTypeOther = null,
    List<string>? RoofType = null,
    string? RoofTypeOther = null,
    // Area
    List<CondoAppraisalAreaDetailDto>? AreaDetails = null,
    decimal? TotalBuildingArea = null,
    // Legal Restrictions
    bool? IsExpropriated = null,
    string? ExpropriationRemark = null,
    bool? IsInExpropriationLine = null,
    string? ExpropriationLineRemark = null,
    string? RoyalDecree = null,
    string? OtherLegalLimitations = null,
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
    string? RoadPassInFrontOfLand = null,
    List<string>? TransportationAccessType = null,
    string? TransportationAccessTypeOther = null,
    // Facilities & Environment
    List<string>? FacilityType = null,
    string? FacilityTypeOther = null,
    List<string>? EnvironmentType = null,
    string? EnvironmentTypeOther = null,
    // Pricing
    bool? IsMissingFromSurvey = null,
    decimal? GovernmentPricePerSqm = null,
    decimal? GovernmentPrice = null,
    // BuildingInsurancePrice is derived server-side from FireInsuranceCode × UsableArea.
    string? FireInsuranceCode = null,
    // Appraiser-keyed coverage; null means "use the derived figure".
    decimal? BuildingInsurancePriceOverride = null,
    // Other
    string? Remark = null,
    // Construction Inspection (null = no-op)
    ConstructionInspectionData? ConstructionInspection = null,
    // Rented out to others: lease agreement & rental info (sent when IsRentedOut == true)
    bool? IsRentedOut = null,
    Shared.LeaseAgreementData? LeaseAgreement = null,
    Shared.RentalInfoData? RentalInfo = null
);
