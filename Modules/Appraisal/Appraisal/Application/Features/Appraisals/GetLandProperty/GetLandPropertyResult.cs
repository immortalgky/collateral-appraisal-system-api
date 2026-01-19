namespace Appraisal.Application.Features.Appraisals.GetLandProperty;

/// <summary>
/// Result of getting a land property
/// </summary>
public record GetLandPropertyResult
{
    // Property Info
    public Guid PropertyId { get; init; }
    public Guid AppraisalId { get; init; }
    public int SequenceNumber { get; init; }
    public string PropertyType { get; init; } = null!;
    public string? Description { get; init; }

    // Land Detail Info
    public Guid? LandDetailId { get; init; }
    public string? PropertyName { get; init; }
    public string? LandOffice { get; init; }
    public string? LandDescription { get; init; }

    // Owner Details
    public string? OwnerName { get; init; }
    public bool IsOwnerVerified { get; init; }
    public bool HasObligation { get; init; }
    public string? ObligationDetails { get; init; }

    // Location
    public string? Street { get; init; }
    public string? Soi { get; init; }
    public string? Village { get; init; }
    public string? SubDistrict { get; init; }
    public string? District { get; init; }
    public string? Province { get; init; }

    // Coordinates
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }

    // Deocument Verification
    public bool? IsLandLocationVerified { get; init; }
    public string? LandCheckMethodType { get; init; }
    public string? LandCheckMethodTypeOther { get; init; }

    public decimal? DistanceFromMainRoad { get; init; }
    public string? AddressLocation { get; init; }

    // Land Characteristics
    public string? LandShapeType { get; init; }
    public string? UrbanPlanningType { get; init; }
    public List<string>? LandZoneType { get; init; }
    public List<string>? PlotLocationType { get; init; }
    public string? PlotLocationTypeOther { get; init; }
    public string? LandFillType { get; init; }
    public string? LandFillTypeOther { get; init; }
    public decimal? LandFillPercent { get; init; }
    public decimal? SoilLevel { get; init; }
    
    // Road Access
    public decimal? AccessRoadWidth { get; init; }
    public short? RightOfWay { get; init; }
    public decimal? RoadFrontage { get; init; }
    public int? NumberOfSidesFacingRoad { get; init; }
    public string? RoadPassInFrontOfLand { get; init; }
    public string? LandAccessibilityType { get; init; }
    public string? LandAccessibilityRemark { get; init; }
    public string? RoadSurfaceType { get; init; }
    public string? RoadSurfaceTypeOther { get; init; }

    // Utilities & Infrastructure
    public bool? HasElectricity { get; init; }
    public decimal? ElectricityDistance { get; init; }
    public List<string>? PublicUtilityType { get; init; }
    public string? PublicUtilityTypeOther { get; init; }
    public List<string>? LandUseType { get; init; }
    public string? LandUseTypeOther { get; init; }
    public List<string>? LandEntranceExitType { get; init; }
    public string? LandEntranceExitTypeOther { get; init; }
    public List<string>? TransportationAccessType { get; init; }
    public string? TransportationAccessTypeOther { get; init; }
    public string? PropertyAnticipationType { get; init; }

    // Legal Information
    public bool? IsExpropriated { get; init; }
    public string? ExpropriationRemark { get; init; }
    public bool? IsInExpropriationLine { get; init; }
    public string? ExpropriationLineRemark { get; init; }
    public string? RoyalDecree { get; init; }
    public bool? IsEncroached { get; init; }
    public string? EncroachmentRemark { get; init; }
    public decimal? EncroachmentArea { get; init; }
    public bool? IsLandlocked { get; init; }
    public string? LandlockedRemark { get; init; }
    public bool? IsForestBoundary { get; init; }
    public string? ForestBoundaryRemark { get; init; }
    public string? OtherLegalLimitations { get; init; }
    public List<string>? EvictionType { get; init; }
    public string? EvictionTypeOther { get; init; }
    public string? AllocationType { get; init; }

    // Adjacent Boundaries
    public string? NorthAdjacentArea { get; init; }
    public decimal? NorthBoundaryLength { get; init; }
    public string? SouthAdjacentArea { get; init; }
    public decimal? SouthBoundaryLength { get; init; }
    public string? EastAdjacentArea { get; init; }
    public decimal? EastBoundaryLength { get; init; }
    public string? WestAdjacentArea { get; init; }
    public decimal? WestBoundaryLength { get; init; }



    // Other
    public decimal? PondArea { get; init; }
    public decimal? PondDepth { get; init; }
    public bool? HasBuilding { get; init; }
    public string? HasBuildingOther { get; init; }
    public string? Remark { get; init; }
}
