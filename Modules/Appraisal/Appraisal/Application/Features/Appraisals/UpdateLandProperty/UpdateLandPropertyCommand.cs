namespace Appraisal.Application.Features.Appraisals.UpdateLandProperty;

/// <summary>
/// Command to update a land property detail
/// </summary>
public record UpdateLandPropertyCommand(
    Guid AppraisalId,
    Guid PropertyId,
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
    // Owner Details
    string? OwnerName = null,
    bool? IsOwnerVerified = null,
    bool? HasObligation = null,
    string? ObligationDetails = null,
    // Document Verification
    bool? IsLandLocationVerified = null,
    string? LandCheckMethodType = null,
    string? LandCheckMethodTypeOther = null,
    // Location Details
    string? Street = null,
    string? Soi = null,
    decimal? DistanceFromMainRoad = null,
    string? Village = null,
    string? AddressLocation = null,
    // Land Characteristics
    string? LandShapeType = null,
    string? UrbanPlanningType = null,
    List<string>? LandZoneType = null,
    List<string>? PlotLocationType = null,
    string? PlotLocationTypeOther = null,
    string? LandFillType = null,
    string? LandFillTypeOther = null,
    decimal? LandFillPercent = null,
    decimal? SoilLevel = null,
    // Road Access
    decimal? AccessRoadWidth = null,
    decimal? RightOfWay = null,
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
    string? PropertyAnticipationType = null,
    // Legal Restrictions
    bool? IsExpropriated = null,
    string? ExpropriationRemark = null,
    bool? IsInExpropriationLine = null,
    string? ExpropriationLineRemark = null,
    string? RoyalDecree = null,
    bool? IsEncroached = null,
    string? EncroachmentRemark = null,
    decimal? EncroachmentArea = null,
    bool? IsLandlocked = null,
    string? LandlockedRemark = null,
    bool? IsForestBoundary = null,
    string? ForestBoundaryRemark = null,
    string? OtherLegalLimitations = null,
    List<string>? EvictionType = null,
    string? EvictionTypeOther = null,
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
    string? Remark = null
) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;
