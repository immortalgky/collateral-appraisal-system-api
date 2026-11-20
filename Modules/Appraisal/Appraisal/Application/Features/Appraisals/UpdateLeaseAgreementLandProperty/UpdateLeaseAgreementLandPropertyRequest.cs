using Appraisal.Application.Features.Appraisals.CreateLandProperty;
using Appraisal.Application.Features.Appraisals.Shared;

namespace Appraisal.Application.Features.Appraisals.UpdateLeaseAgreementLandProperty;

/// <summary>
/// Request to update a lease agreement land property detail
/// </summary>
public record UpdateLeaseAgreementLandPropertyRequest(
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
    string? Remark = null,
    // Land Titles
    List<LandTitleItemData>? Titles = null,
    // Area deductions (null = no-op, empty list = clear all)
    List<LandAreaDeductionData>? LandAreaDeductions = null,
    LeaseAgreementData? LeaseAgreement = null,
    RentalInfoData? RentalInfo = null
);
