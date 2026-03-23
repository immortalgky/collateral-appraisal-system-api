namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Land property appraisal details including location, access, utilities, legal restrictions, and boundaries.
/// 1:1 relationship with AppraisalProperty (PropertyType = Land)
/// Naming aligned with LandAndBuildingAppraisalDetail for consistency.
/// </summary>
public class LandAppraisalDetail : Entity<Guid>
{
    private readonly List<LandTitle> _titles = [];
    public IReadOnlyList<LandTitle> Titles => _titles.AsReadOnly();

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
    public string? OwnerName { get; private set; } = null!;
    public bool? IsOwnerVerified { get; private set; }
    public bool? HasObligation { get; private set; }
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
    public short? RightOfWay { get; private set; }
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
    public bool? IsExpropriated { get; private set; }
    public string? ExpropriationRemark { get; private set; }
    public bool? IsInExpropriationLine { get; private set; }
    public string? ExpropriationLineRemark { get; private set; }
    public string? RoyalDecree { get; private set; }
    public bool? IsEncroached { get; private set; }
    public string? EncroachmentRemark { get; private set; }
    public decimal? EncroachmentArea { get; private set; }
    public bool? IsLandlocked { get; private set; }
    public string? LandlockedRemark { get; private set; }
    public bool? IsForestBoundary { get; private set; }
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
    public bool? HasBuilding { get; private set; }
    public string? HasBuildingOther { get; private set; }
    public string? Remark { get; private set; }

    // Computed: total land area across all titles
    public decimal TotalLandAreaInSqWa =>
        _titles.Where(t => t.Area != null && t.Area.HasValue)
               .Sum(t => t.Area!.TotalSquareWa ?? 0);

    private LandAppraisalDetail()
    {
        // For EF Core
    }

    public static LandAppraisalDetail Create(Guid appraisalPropertyId)
    {
        return new LandAppraisalDetail
        {
            AppraisalPropertyId = appraisalPropertyId
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
        short? rightOfWay = null,
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
        OwnerName = ownerName;
        IsOwnerVerified = isOwnerVerified;
        HasObligation = hasObligation;
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
        IsExpropriated = isExpropriated;
        ExpropriationRemark = expropriationRemark;
        IsInExpropriationLine = isInExpropriationLine;
        ExpropriationLineRemark = expropriationLineRemark;
        RoyalDecree = royalDecree;
        IsEncroached = isEncroached;
        EncroachmentRemark = encroachmentRemark;
        EncroachmentArea = encroachmentArea;
        IsLandlocked = isLandlocked;
        LandlockedRemark = landlockedRemark;
        IsForestBoundary = isForestBoundary;
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
        HasBuilding = hasBuilding;
        HasBuildingOther = hasBuildingOther;
        Remark = remark;
    }

    public static LandAppraisalDetail CopyFrom(LandAppraisalDetail source, Guid newPropertyId)
    {
        var copy = new LandAppraisalDetail
        {
            AppraisalPropertyId = newPropertyId,
            PropertyName = source.PropertyName,
            LandDescription = source.LandDescription,
            Coordinates = source.Coordinates is not null
                ? GpsCoordinate.Create(source.Coordinates.Latitude, source.Coordinates.Longitude)
                : null,
            Address = source.Address is not null
                ? AdministrativeAddress.Create(source.Address.SubDistrict, source.Address.District, source.Address.Province, source.Address.LandOffice)
                : null,
            OwnerName = source.OwnerName,
            IsOwnerVerified = source.IsOwnerVerified,
            HasObligation = source.HasObligation,
            ObligationDetails = source.ObligationDetails,
            IsLandLocationVerified = source.IsLandLocationVerified,
            LandCheckMethodType = source.LandCheckMethodType,
            LandCheckMethodTypeOther = source.LandCheckMethodTypeOther,
            Street = source.Street,
            Soi = source.Soi,
            DistanceFromMainRoad = source.DistanceFromMainRoad,
            Village = source.Village,
            AddressLocation = source.AddressLocation,
            LandShapeType = source.LandShapeType,
            UrbanPlanningType = source.UrbanPlanningType,
            LandZoneType = source.LandZoneType?.ToList(),
            PlotLocationType = source.PlotLocationType?.ToList(),
            PlotLocationTypeOther = source.PlotLocationTypeOther,
            LandFillType = source.LandFillType,
            LandFillTypeOther = source.LandFillTypeOther,
            LandFillPercent = source.LandFillPercent,
            SoilLevel = source.SoilLevel,
            AccessRoadWidth = source.AccessRoadWidth,
            RightOfWay = source.RightOfWay,
            RoadFrontage = source.RoadFrontage,
            NumberOfSidesFacingRoad = source.NumberOfSidesFacingRoad,
            RoadPassInFrontOfLand = source.RoadPassInFrontOfLand,
            LandAccessibilityType = source.LandAccessibilityType,
            LandAccessibilityRemark = source.LandAccessibilityRemark,
            RoadSurfaceType = source.RoadSurfaceType,
            RoadSurfaceTypeOther = source.RoadSurfaceTypeOther,
            HasElectricity = source.HasElectricity,
            ElectricityDistance = source.ElectricityDistance,
            PublicUtilityType = source.PublicUtilityType?.ToList(),
            PublicUtilityTypeOther = source.PublicUtilityTypeOther,
            LandUseType = source.LandUseType?.ToList(),
            LandUseTypeOther = source.LandUseTypeOther,
            LandEntranceExitType = source.LandEntranceExitType?.ToList(),
            LandEntranceExitTypeOther = source.LandEntranceExitTypeOther,
            TransportationAccessType = source.TransportationAccessType?.ToList(),
            TransportationAccessTypeOther = source.TransportationAccessTypeOther,
            PropertyAnticipationType = source.PropertyAnticipationType,
            IsExpropriated = source.IsExpropriated,
            ExpropriationRemark = source.ExpropriationRemark,
            IsInExpropriationLine = source.IsInExpropriationLine,
            ExpropriationLineRemark = source.ExpropriationLineRemark,
            RoyalDecree = source.RoyalDecree,
            IsEncroached = source.IsEncroached,
            EncroachmentRemark = source.EncroachmentRemark,
            EncroachmentArea = source.EncroachmentArea,
            IsLandlocked = source.IsLandlocked,
            LandlockedRemark = source.LandlockedRemark,
            IsForestBoundary = source.IsForestBoundary,
            ForestBoundaryRemark = source.ForestBoundaryRemark,
            OtherLegalLimitations = source.OtherLegalLimitations,
            EvictionType = source.EvictionType?.ToList(),
            EvictionTypeOther = source.EvictionTypeOther,
            AllocationType = source.AllocationType,
            NorthAdjacentArea = source.NorthAdjacentArea,
            NorthBoundaryLength = source.NorthBoundaryLength,
            SouthAdjacentArea = source.SouthAdjacentArea,
            SouthBoundaryLength = source.SouthBoundaryLength,
            EastAdjacentArea = source.EastAdjacentArea,
            EastBoundaryLength = source.EastBoundaryLength,
            WestAdjacentArea = source.WestAdjacentArea,
            WestBoundaryLength = source.WestBoundaryLength,
            PondArea = source.PondArea,
            PondDepth = source.PondDepth,
            HasBuilding = source.HasBuilding,
            HasBuildingOther = source.HasBuildingOther,
            Remark = source.Remark
        };

        foreach (var title in source.Titles)
        {
            var titleCopy = LandTitle.Create(copy.Id, title.TitleNumber, title.TitleType);
            var areaCopy = title.Area is not null
                ? LandArea.Create(title.Area.Rai, title.Area.Ngan, title.Area.SquareWa)
                : null;
            titleCopy.Update(
                title.BookNumber, title.PageNumber, title.LandParcelNumber,
                title.SurveyNumber, title.MapSheetNumber, title.Rawang,
                title.AerialMapName, title.AerialMapNumber, areaCopy,
                title.BoundaryMarkerType, title.BoundaryMarkerRemark,
                title.DocumentValidationResultType, title.IsMissingFromSurvey,
                title.GovernmentPricePerSqWa, title.GovernmentPrice, title.Remark);
            copy._titles.Add(titleCopy);
        }

        return copy;
    }

    public void AddTitle(LandTitle title)
    {
        _titles.Add(title);
    }

    public void RemoveTitle(Guid titleId)
    {
        var title = _titles.FirstOrDefault(t => t.Id == titleId);
        if (title != null) _titles.Remove(title);
    }

    public void UpdateTitle(LandTitle updatedTitle)
    {
        var title = _titles.FirstOrDefault(t => t.Id == updatedTitle.Id);
        if (title != null)
            title.Update(
                updatedTitle.BookNumber,
                updatedTitle.PageNumber,
                updatedTitle.LandParcelNumber,
                updatedTitle.SurveyNumber,
                updatedTitle.MapSheetNumber,
                updatedTitle.Rawang,
                updatedTitle.AerialMapName,
                updatedTitle.AerialMapNumber,
                updatedTitle.Area,
                updatedTitle.BoundaryMarkerType,
                updatedTitle.BoundaryMarkerRemark,
                updatedTitle.DocumentValidationResultType,
                updatedTitle.IsMissingFromSurvey,
                updatedTitle.GovernmentPricePerSqWa,
                updatedTitle.GovernmentPrice,
                updatedTitle.Remark
            );
    }
}