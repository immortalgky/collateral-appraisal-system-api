namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Land property appraisal details including location, access, utilities, legal restrictions, and boundaries.
/// 1:1 relationship with AppraisalProperty (PropertyType = Land)
/// Naming aligned with LandAndBuildingAppraisalDetail for consistency.
/// </summary>
public class LandAppraisalDetail : Entity<Guid>
{
    // Foreign Key - 1:1 with AppraisalProperties
    public Guid AppraisalPropertyId { get; private set; }

    // Property Identification
    public string? PropertyName { get; private set; }
    public string? LandDescription { get; private set; }

    // GPS Coordinates (Value Object)
    public GpsCoordinate? Coordinates { get; private set; }

    // Administrative Address (Value Object)
    public AdministrativeAddress? Address { get; private set; }

    // Owner
    public string OwnerName { get; private set; } = null!;
    public bool IsOwnerVerified { get; private set; }
    public bool HasObligation { get; private set; }
    public string? ObligationDetails { get; private set; }

    // Document Verification
    public bool? IsLandLocationVerified { get; private set; }
    public string? LandCheckMethodType { get; private set; }
    public string? LandCheckMethodTypeOther { get; private set; }

    // Location Details
    public string? Street { get; private set; }
    public string? Soi { get; private set; }
    public decimal? DistanceFromMainRoad { get; private set; }
    public string? Village { get; private set; }
    public string? AddressLocation { get; private set; }

    // Land Characteristics
    public string? LandShapeType { get; private set; }
    public string? UrbanPlanningType { get; private set; }
    public List<string>? LandZoneType { get; private set; }
    public List<string>? PlotLocationType { get; private set; }
    public string? PlotLocationTypeOther { get; private set; }
    public string? LandFillType { get; private set; }
    public string? LandFillTypeOther { get; private set; }
    public decimal? LandFillPercent { get; private set; }
    public decimal? SoilLevel { get; private set; }

    // Road Access
    public decimal? AccessRoadWidth { get; private set; }
    public decimal? RightOfWay { get; private set; }
    public decimal? RoadFrontage { get; private set; }
    public int? NumberOfSidesFacingRoad { get; private set; }
    public string? RoadPassInFrontOfLand { get; private set; }
    public string? LandAccessibilityType { get; private set; }
    public string? LandAccessibilityRemark { get; private set; }
    public string? RoadSurfaceType { get; private set; }
    public string? RoadSurfaceTypeOther { get; private set; }

    // Utilities & Infrastructure
    public bool? HasElectricity { get; private set; }
    public decimal? ElectricityDistance { get; private set; }
    public List<string>? PublicUtilityType { get; private set; }
    public string? PublicUtilityTypeOther { get; private set; }
    public List<string>? LandUseType { get; private set; }
    public string? LandUseTypeOther { get; private set; }
    public List<string>? LandEntranceExitType { get; private set; }
    public string? LandEntranceExitTypeOther { get; private set; }
    public List<string>? TransportationAccessType { get; private set; }
    public string? TransportationAccessTypeOther { get; private set; }
    public string? PropertyAnticipationType { get; private set; }

    // Legal Restrictions
    public bool IsExpropriated { get; private set; }
    public string? ExpropriationRemark { get; private set; }
    public bool IsInExpropriationLine { get; private set; }
    public string? ExpropriationLineRemark { get; private set; }
    public string? RoyalDecree { get; private set; }
    public bool IsEncroached { get; private set; }
    public string? EncroachmentRemark { get; private set; }
    public decimal? EncroachmentArea { get; private set; }
    public bool IsLandlocked { get; private set; }
    public string? LandlockedRemark { get; private set; }
    public bool IsForestBoundary { get; private set; }
    public string? ForestBoundaryRemark { get; private set; }
    public string? OtherLegalLimitations { get; private set; }
    public List<string>? EvictionType { get; private set; }
    public string? EvictionTypeOther { get; private set; }
    public string? AllocationType { get; private set; }

    // Adjacent Boundaries (North/South/East/West)
    public string? NorthAdjacentArea { get; private set; }
    public decimal? NorthBoundaryLength { get; private set; }
    public string? SouthAdjacentArea { get; private set; }
    public decimal? SouthBoundaryLength { get; private set; }
    public string? EastAdjacentArea { get; private set; }
    public decimal? EastBoundaryLength { get; private set; }
    public string? WestAdjacentArea { get; private set; }
    public decimal? WestBoundaryLength { get; private set; }

    // Other Features
    public decimal? PondArea { get; private set; }
    public decimal? PondDepth { get; private set; }
    public bool HasBuilding { get; private set; }
    public string? HasBuildingOther { get; private set; }
    public string? Remark { get; private set; }

    private LandAppraisalDetail()
    {
        // For EF Core
    }

    public static LandAppraisalDetail Create(
        Guid appraisalPropertyId,
        string ownerName,
        Guid createdBy)
    {
        return new LandAppraisalDetail
        {
            //Id = Guid.NewGuid(),
            AppraisalPropertyId = appraisalPropertyId,
            OwnerName = ownerName
        };
    }

    /// <summary>
    /// Update all land detail fields
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public void Update(
        // Property Identification
        string? propertyName = null,
        string? landDescription = null,
        GpsCoordinate? coordinates = null,
        AdministrativeAddress? address = null,
        // Owner
        string? ownerName = null,
        bool? isOwnerVerified = null,
        bool? hasObligation = null,
        string? obligationDetails = null,
        // Document Verification
        bool? isLandLocationVerified = null,
        string? landCheckMethodType = null,
        string? landCheckMethodTypeOther = null,
        // Location Details
        string? street = null,
        string? soi = null,
        decimal? distanceFromMainRoad = null,
        string? village = null,
        string? addressLocation = null,
        // Land Characteristics
        string? landShapeType = null,
        string? urbanPlanningType = null,
        List<string>? landZoneType = null,
        List<string>? plotLocationType = null,
        string? plotLocationTypeOther = null,
        string? landFillType = null,
        string? landFillTypeOther = null,
        decimal? landFillPercent = null,
        decimal? soilLevel = null,
        // Road Access
        decimal? accessRoadWidth = null,
        decimal? rightOfWay = null,
        decimal? roadFrontage = null,
        int? numberOfSidesFacingRoad = null,
        string? roadPassInFrontOfLand = null,
        string? landAccessibilityType = null,
        string? landAccessibilityRemark = null,
        string? roadSurfaceType = null,
        string? roadSurfaceTypeOther = null,
        // Utilities & Infrastructure
        bool? hasElectricity = null,
        decimal? electricityDistance = null,
        List<string>? publicUtilityType = null,
        string? publicUtilityTypeOther = null,
        List<string>? landUseType = null,
        string? landUseTypeOther = null,
        List<string>? landEntranceExitType = null,
        string? landEntranceExitTypeOther = null,
        List<string>? transportationAccessType = null,
        string? transportationAccessTypeOther = null,
        string? propertyAnticipationType = null,
        // Legal Restrictions
        bool? isExpropriated = null,
        string? expropriationRemark = null,
        bool? isInExpropriationLine = null,
        string? expropriationLineRemark = null,
        string? royalDecree = null,
        bool? isEncroached = null,
        string? encroachmentRemark = null,
        decimal? encroachmentArea = null,
        bool? isLandlocked = null,
        string? landlockedRemark = null,
        bool? isForestBoundary = null,
        string? forestBoundaryRemark = null,
        string? otherLegalLimitations = null,
        List<string>? evictionType = null,
        string? evictionTypeOther = null,
        string? allocationType = null,
        // Adjacent Boundaries
        string? northAdjacentArea = null,
        decimal? northBoundaryLength = null,
        string? southAdjacentArea = null,
        decimal? southBoundaryLength = null,
        string? eastAdjacentArea = null,
        decimal? eastBoundaryLength = null,
        string? westAdjacentArea = null,
        decimal? westBoundaryLength = null,
        // Other Features
        decimal? pondArea = null,
        decimal? pondDepth = null,
        bool? hasBuilding = null,
        string? hasBuildingOther = null,
        string? remark = null)
    {
        // Property Identification
        PropertyName = propertyName;
        LandDescription = landDescription;
        Coordinates = coordinates;
        Address = address;

        // Owner (OwnerName is required, keep null check; bool fields keep check since non-nullable)
        if (ownerName is not null) OwnerName = ownerName;
        if (isOwnerVerified.HasValue) IsOwnerVerified = isOwnerVerified.Value;
        if (hasObligation.HasValue) HasObligation = hasObligation.Value;
        ObligationDetails = obligationDetails;

        // Document Verification
        IsLandLocationVerified = isLandLocationVerified;
        LandCheckMethodType = landCheckMethodType;
        LandCheckMethodTypeOther = landCheckMethodTypeOther;

        // Location Details
        Street = street;
        Soi = soi;
        DistanceFromMainRoad = distanceFromMainRoad;
        Village = village;
        AddressLocation = addressLocation;

        // Land Characteristics
        LandShapeType = landShapeType;
        UrbanPlanningType = urbanPlanningType;
        LandZoneType = landZoneType;
        PlotLocationType = plotLocationType;
        PlotLocationTypeOther = plotLocationTypeOther;
        LandFillType = landFillType;
        LandFillTypeOther = landFillTypeOther;
        LandFillPercent = landFillPercent;
        SoilLevel = soilLevel;

        // Road Access
        AccessRoadWidth = accessRoadWidth;
        RightOfWay = rightOfWay;
        RoadFrontage = roadFrontage;
        NumberOfSidesFacingRoad = numberOfSidesFacingRoad;
        RoadPassInFrontOfLand = roadPassInFrontOfLand;
        LandAccessibilityType = landAccessibilityType;
        LandAccessibilityRemark = landAccessibilityRemark;
        RoadSurfaceType = roadSurfaceType;
        RoadSurfaceTypeOther = roadSurfaceTypeOther;

        // Utilities & Infrastructure
        HasElectricity = hasElectricity;
        ElectricityDistance = electricityDistance;
        PublicUtilityType = publicUtilityType;
        PublicUtilityTypeOther = publicUtilityTypeOther;
        LandUseType = landUseType;
        LandUseTypeOther = landUseTypeOther;
        LandEntranceExitType = landEntranceExitType;
        LandEntranceExitTypeOther = landEntranceExitTypeOther;
        TransportationAccessType = transportationAccessType;
        TransportationAccessTypeOther = transportationAccessTypeOther;
        PropertyAnticipationType = propertyAnticipationType;

        // Legal Restrictions (non-nullable bool fields keep check)
        if (isExpropriated.HasValue) IsExpropriated = isExpropriated.Value;
        ExpropriationRemark = expropriationRemark;
        if (isInExpropriationLine.HasValue) IsInExpropriationLine = isInExpropriationLine.Value;
        ExpropriationLineRemark = expropriationLineRemark;
        RoyalDecree = royalDecree;
        if (isEncroached.HasValue) IsEncroached = isEncroached.Value;
        EncroachmentRemark = encroachmentRemark;
        EncroachmentArea = encroachmentArea;
        if (isLandlocked.HasValue) IsLandlocked = isLandlocked.Value;
        LandlockedRemark = landlockedRemark;
        if (isForestBoundary.HasValue) IsForestBoundary = isForestBoundary.Value;
        ForestBoundaryRemark = forestBoundaryRemark;
        OtherLegalLimitations = otherLegalLimitations;
        EvictionType = evictionType;
        EvictionTypeOther = evictionTypeOther;
        AllocationType = allocationType;

        // Adjacent Boundaries
        NorthAdjacentArea = northAdjacentArea;
        NorthBoundaryLength = northBoundaryLength;
        SouthAdjacentArea = southAdjacentArea;
        SouthBoundaryLength = southBoundaryLength;
        EastAdjacentArea = eastAdjacentArea;
        EastBoundaryLength = eastBoundaryLength;
        WestAdjacentArea = westAdjacentArea;
        WestBoundaryLength = westBoundaryLength;

        // Other Features
        PondArea = pondArea;
        PondDepth = pondDepth;
        if (hasBuilding.HasValue) HasBuilding = hasBuilding.Value;
        HasBuildingOther = hasBuildingOther;
        Remark = remark;
    }
}