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
    public Address? Address { get; private set; }
    public string? LandOffice { get; private set; }

    // Dopa Address (Value Object)
    public Address? DopaAddress { get; private set; }

    // Owner
    public string? OwnerName { get; private set; } = null!;
    public bool? IsOwnerVerified { get; private set; }
    public string? HasObligation { get; private set; }
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
    public string? LandShapeTypeOther { get; private set; }
    public string? UrbanPlanningType { get; private set; }
    public List<string>? LandZoneType { get; private set; }
    public string? LandZoneTypeOther { get; private set; }
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
    public string? PropertyAnticipationTypeOther { get; private set; }

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

    // Rental Flag
    public bool? IsRentedOut { get; private set; }

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
        Address? address = null,
        // Owner
        string? ownerName = null,
        bool? isOwnerVerified = null,
        string? hasObligation = null,
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
        string? landShapeTypeOther = null,
        string? urbanPlanningType = null,
        List<string>? landZoneType = null,
        string? landZoneTypeOther = null,
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
        string? propertyAnticipationTypeOther = null,
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
        string? remark = null,
        // Rental Flag
        bool? isRentedOut = null,
        // Address scalar + Dopa (at end to avoid breaking existing positional callers)
        string? landOffice = null,
        Address? dopaAddress = null)
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
        LandShapeTypeOther = landShapeTypeOther;
        UrbanPlanningType = urbanPlanningType;
        LandZoneType = landZoneType;
        LandZoneTypeOther = landZoneTypeOther;
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
        PropertyAnticipationTypeOther = propertyAnticipationTypeOther;

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

        // Rental Flag
        IsRentedOut = isRentedOut;

        // Address scalar + Dopa
        LandOffice = landOffice;
        DopaAddress = dopaAddress;
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
                ? Address.Create(source.Address.SubDistrict, source.Address.District, source.Address.Province)
                : null,
            LandOffice = source.LandOffice,
            DopaAddress = source.DopaAddress is not null
                ? Address.Create(source.DopaAddress.SubDistrict, source.DopaAddress.District, source.DopaAddress.Province)
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
            LandShapeTypeOther = source.LandShapeTypeOther,
            UrbanPlanningType = source.UrbanPlanningType,
            LandZoneType = source.LandZoneType?.ToList(),
            LandZoneTypeOther = source.LandZoneTypeOther,
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
            PropertyAnticipationTypeOther = source.PropertyAnticipationTypeOther,
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
            Remark = source.Remark,
            IsRentedOut = source.IsRentedOut
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

    /// <summary>
    /// Narrow update for the PMA save path — touches ONLY the fields the PMA form authors
    /// (owner and address; prices go through <c>AppraisalProperty.UpdatePrice</c> and titles
    /// through the applier's title sync).
    /// <para>
    /// Deliberately NOT <see cref="Update"/>: that method is a full overwrite of all ~77
    /// properties, so calling it from PMA (which supplies one or two arguments) silently reset
    /// every unsupplied field to null — wiping appraiser-entered land detail including
    /// LandEntranceExitType, LandFillType, UrbanPlanningType and LandUseType. Keep this method
    /// narrow; do not grow it into a second full overwrite.
    /// </para>
    /// </summary>
    public void UpdatePmaFields(
        string? ownerName = null,
        Address? address = null)
    {
        OwnerName = ownerName;
        Address = address;
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

    /// <summary>
    /// Applies admin corrections to this land detail, recording each change in <paramref name="diff"/>.
    /// Only fields the caller actually supplied are touched.
    ///
    /// Deliberately NOT routed through <see cref="Update"/>: that method overwrites all ~77
    /// properties unconditionally, so a partial correction would wipe everything unsupplied — the
    /// data-loss trap documented on <see cref="UpdatePmaFields"/>.
    /// </summary>
    internal void ApplyCorrection(LandCorrection edit, Dictionary<string, object?> diff)
    {
        CorrectionDiff.Apply("Land.PropertyName", PropertyName, edit.PropertyName, v => PropertyName = v, diff);
        CorrectionDiff.Apply("Land.LandDescription", LandDescription, edit.LandDescription, v => LandDescription = v, diff);
        // Coordinates is an immutable record — compare components, rebuild once.
        var latitude = Coordinates?.Latitude;
        var longitude = Coordinates?.Longitude;
        var coordinatesChanged = false;
        CorrectionDiff.Apply("Land.Latitude", latitude, edit.Latitude,
            v => { latitude = v; coordinatesChanged = true; }, diff);
        CorrectionDiff.Apply("Land.Longitude", longitude, edit.Longitude,
            v => { longitude = v; coordinatesChanged = true; }, diff);
        if (coordinatesChanged)
            Coordinates = Domain.Appraisals.GpsCoordinate.Create(latitude, longitude);

        // Address is an immutable record — compare components, rebuild once.
        var subDistrict = Address?.SubDistrict;
        var district = Address?.District;
        var province = Address?.Province;
        var addressChanged = false;
        CorrectionDiff.Apply("Land.SubDistrict", subDistrict, edit.SubDistrict,
            v => { subDistrict = v; addressChanged = true; }, diff);
        CorrectionDiff.Apply("Land.District", district, edit.District,
            v => { district = v; addressChanged = true; }, diff);
        CorrectionDiff.Apply("Land.Province", province, edit.Province,
            v => { province = v; addressChanged = true; }, diff);
        if (addressChanged)
            Address = Domain.Appraisals.Address.Create(subDistrict, district, province);

        CorrectionDiff.Apply("Land.LandOffice", LandOffice, edit.LandOffice, v => LandOffice = v, diff);
        // DopaAddress is an immutable record — compare components, rebuild once.
        var dopaSubDistrict = DopaAddress?.SubDistrict;
        var dopaDistrict = DopaAddress?.District;
        var dopaProvince = DopaAddress?.Province;
        var dopaAddressChanged = false;
        CorrectionDiff.Apply("Land.DopaSubDistrict", dopaSubDistrict, edit.DopaSubDistrict,
            v => { dopaSubDistrict = v; dopaAddressChanged = true; }, diff);
        CorrectionDiff.Apply("Land.DopaDistrict", dopaDistrict, edit.DopaDistrict,
            v => { dopaDistrict = v; dopaAddressChanged = true; }, diff);
        CorrectionDiff.Apply("Land.DopaProvince", dopaProvince, edit.DopaProvince,
            v => { dopaProvince = v; dopaAddressChanged = true; }, diff);
        if (dopaAddressChanged)
            DopaAddress = Domain.Appraisals.Address.Create(dopaSubDistrict, dopaDistrict, dopaProvince);

        CorrectionDiff.Apply("Land.OwnerName", OwnerName, edit.OwnerName, v => OwnerName = v, diff);
        CorrectionDiff.Apply("Land.IsOwnerVerified", IsOwnerVerified, edit.IsOwnerVerified, v => IsOwnerVerified = v, diff);
        CorrectionDiff.Apply("Land.HasObligation", HasObligation, edit.HasObligation, v => HasObligation = v, diff);
        CorrectionDiff.Apply("Land.ObligationDetails", ObligationDetails, edit.ObligationDetails, v => ObligationDetails = v, diff);
        CorrectionDiff.Apply("Land.IsLandLocationVerified", IsLandLocationVerified, edit.IsLandLocationVerified, v => IsLandLocationVerified = v, diff);
        CorrectionDiff.Apply("Land.LandCheckMethodType", LandCheckMethodType, edit.LandCheckMethodType, v => LandCheckMethodType = v, diff);
        CorrectionDiff.Apply("Land.LandCheckMethodTypeOther", LandCheckMethodTypeOther, edit.LandCheckMethodTypeOther, v => LandCheckMethodTypeOther = v, diff);
        CorrectionDiff.Apply("Land.Street", Street, edit.Street, v => Street = v, diff);
        CorrectionDiff.Apply("Land.Soi", Soi, edit.Soi, v => Soi = v, diff);
        CorrectionDiff.Apply("Land.DistanceFromMainRoad", DistanceFromMainRoad, edit.DistanceFromMainRoad, v => DistanceFromMainRoad = v, diff);
        CorrectionDiff.Apply("Land.Village", Village, edit.Village, v => Village = v, diff);
        CorrectionDiff.Apply("Land.AddressLocation", AddressLocation, edit.AddressLocation, v => AddressLocation = v, diff);
        CorrectionDiff.Apply("Land.LandShapeType", LandShapeType, edit.LandShapeType, v => LandShapeType = v, diff);
        CorrectionDiff.Apply("Land.LandShapeTypeOther", LandShapeTypeOther, edit.LandShapeTypeOther, v => LandShapeTypeOther = v, diff);
        CorrectionDiff.Apply("Land.UrbanPlanningType", UrbanPlanningType, edit.UrbanPlanningType, v => UrbanPlanningType = v, diff);
        CorrectionDiff.ApplyList("Land.LandZoneType", LandZoneType, edit.LandZoneType, v => LandZoneType = v, diff);
        CorrectionDiff.Apply("Land.LandZoneTypeOther", LandZoneTypeOther, edit.LandZoneTypeOther, v => LandZoneTypeOther = v, diff);
        CorrectionDiff.ApplyList("Land.PlotLocationType", PlotLocationType, edit.PlotLocationType, v => PlotLocationType = v, diff);
        CorrectionDiff.Apply("Land.PlotLocationTypeOther", PlotLocationTypeOther, edit.PlotLocationTypeOther, v => PlotLocationTypeOther = v, diff);
        CorrectionDiff.Apply("Land.LandFillType", LandFillType, edit.LandFillType, v => LandFillType = v, diff);
        CorrectionDiff.Apply("Land.LandFillTypeOther", LandFillTypeOther, edit.LandFillTypeOther, v => LandFillTypeOther = v, diff);
        CorrectionDiff.Apply("Land.LandFillPercent", LandFillPercent, edit.LandFillPercent, v => LandFillPercent = v, diff);
        CorrectionDiff.Apply("Land.SoilLevel", SoilLevel, edit.SoilLevel, v => SoilLevel = v, diff);
        CorrectionDiff.Apply("Land.AccessRoadWidth", AccessRoadWidth, edit.AccessRoadWidth, v => AccessRoadWidth = v, diff);
        CorrectionDiff.Apply("Land.RightOfWay", RightOfWay, edit.RightOfWay, v => RightOfWay = v, diff);
        CorrectionDiff.Apply("Land.RoadFrontage", RoadFrontage, edit.RoadFrontage, v => RoadFrontage = v, diff);
        CorrectionDiff.Apply("Land.NumberOfSidesFacingRoad", NumberOfSidesFacingRoad, edit.NumberOfSidesFacingRoad, v => NumberOfSidesFacingRoad = v, diff);
        CorrectionDiff.Apply("Land.RoadPassInFrontOfLand", RoadPassInFrontOfLand, edit.RoadPassInFrontOfLand, v => RoadPassInFrontOfLand = v, diff);
        CorrectionDiff.Apply("Land.LandAccessibilityType", LandAccessibilityType, edit.LandAccessibilityType, v => LandAccessibilityType = v, diff);
        CorrectionDiff.Apply("Land.LandAccessibilityRemark", LandAccessibilityRemark, edit.LandAccessibilityRemark, v => LandAccessibilityRemark = v, diff);
        CorrectionDiff.Apply("Land.RoadSurfaceType", RoadSurfaceType, edit.RoadSurfaceType, v => RoadSurfaceType = v, diff);
        CorrectionDiff.Apply("Land.RoadSurfaceTypeOther", RoadSurfaceTypeOther, edit.RoadSurfaceTypeOther, v => RoadSurfaceTypeOther = v, diff);
        CorrectionDiff.Apply("Land.HasElectricity", HasElectricity, edit.HasElectricity, v => HasElectricity = v, diff);
        CorrectionDiff.Apply("Land.ElectricityDistance", ElectricityDistance, edit.ElectricityDistance, v => ElectricityDistance = v, diff);
        CorrectionDiff.ApplyList("Land.PublicUtilityType", PublicUtilityType, edit.PublicUtilityType, v => PublicUtilityType = v, diff);
        CorrectionDiff.Apply("Land.PublicUtilityTypeOther", PublicUtilityTypeOther, edit.PublicUtilityTypeOther, v => PublicUtilityTypeOther = v, diff);
        CorrectionDiff.ApplyList("Land.LandUseType", LandUseType, edit.LandUseType, v => LandUseType = v, diff);
        CorrectionDiff.Apply("Land.LandUseTypeOther", LandUseTypeOther, edit.LandUseTypeOther, v => LandUseTypeOther = v, diff);
        CorrectionDiff.ApplyList("Land.LandEntranceExitType", LandEntranceExitType, edit.LandEntranceExitType, v => LandEntranceExitType = v, diff);
        CorrectionDiff.Apply("Land.LandEntranceExitTypeOther", LandEntranceExitTypeOther, edit.LandEntranceExitTypeOther, v => LandEntranceExitTypeOther = v, diff);
        CorrectionDiff.ApplyList("Land.TransportationAccessType", TransportationAccessType, edit.TransportationAccessType, v => TransportationAccessType = v, diff);
        CorrectionDiff.Apply("Land.TransportationAccessTypeOther", TransportationAccessTypeOther, edit.TransportationAccessTypeOther, v => TransportationAccessTypeOther = v, diff);
        CorrectionDiff.Apply("Land.PropertyAnticipationType", PropertyAnticipationType, edit.PropertyAnticipationType, v => PropertyAnticipationType = v, diff);
        CorrectionDiff.Apply("Land.PropertyAnticipationTypeOther", PropertyAnticipationTypeOther, edit.PropertyAnticipationTypeOther, v => PropertyAnticipationTypeOther = v, diff);
        CorrectionDiff.Apply("Land.IsExpropriated", IsExpropriated, edit.IsExpropriated, v => IsExpropriated = v, diff);
        CorrectionDiff.Apply("Land.ExpropriationRemark", ExpropriationRemark, edit.ExpropriationRemark, v => ExpropriationRemark = v, diff);
        CorrectionDiff.Apply("Land.IsInExpropriationLine", IsInExpropriationLine, edit.IsInExpropriationLine, v => IsInExpropriationLine = v, diff);
        CorrectionDiff.Apply("Land.ExpropriationLineRemark", ExpropriationLineRemark, edit.ExpropriationLineRemark, v => ExpropriationLineRemark = v, diff);
        CorrectionDiff.Apply("Land.RoyalDecree", RoyalDecree, edit.RoyalDecree, v => RoyalDecree = v, diff);
        CorrectionDiff.Apply("Land.IsEncroached", IsEncroached, edit.IsEncroached, v => IsEncroached = v, diff);
        CorrectionDiff.Apply("Land.EncroachmentRemark", EncroachmentRemark, edit.EncroachmentRemark, v => EncroachmentRemark = v, diff);
        CorrectionDiff.Apply("Land.EncroachmentArea", EncroachmentArea, edit.EncroachmentArea, v => EncroachmentArea = v, diff);
        CorrectionDiff.Apply("Land.IsLandlocked", IsLandlocked, edit.IsLandlocked, v => IsLandlocked = v, diff);
        CorrectionDiff.Apply("Land.LandlockedRemark", LandlockedRemark, edit.LandlockedRemark, v => LandlockedRemark = v, diff);
        CorrectionDiff.Apply("Land.IsForestBoundary", IsForestBoundary, edit.IsForestBoundary, v => IsForestBoundary = v, diff);
        CorrectionDiff.Apply("Land.ForestBoundaryRemark", ForestBoundaryRemark, edit.ForestBoundaryRemark, v => ForestBoundaryRemark = v, diff);
        CorrectionDiff.Apply("Land.OtherLegalLimitations", OtherLegalLimitations, edit.OtherLegalLimitations, v => OtherLegalLimitations = v, diff);
        CorrectionDiff.ApplyList("Land.EvictionType", EvictionType, edit.EvictionType, v => EvictionType = v, diff);
        CorrectionDiff.Apply("Land.EvictionTypeOther", EvictionTypeOther, edit.EvictionTypeOther, v => EvictionTypeOther = v, diff);
        CorrectionDiff.Apply("Land.AllocationType", AllocationType, edit.AllocationType, v => AllocationType = v, diff);
        CorrectionDiff.Apply("Land.NorthAdjacentArea", NorthAdjacentArea, edit.NorthAdjacentArea, v => NorthAdjacentArea = v, diff);
        CorrectionDiff.Apply("Land.NorthBoundaryLength", NorthBoundaryLength, edit.NorthBoundaryLength, v => NorthBoundaryLength = v, diff);
        CorrectionDiff.Apply("Land.SouthAdjacentArea", SouthAdjacentArea, edit.SouthAdjacentArea, v => SouthAdjacentArea = v, diff);
        CorrectionDiff.Apply("Land.SouthBoundaryLength", SouthBoundaryLength, edit.SouthBoundaryLength, v => SouthBoundaryLength = v, diff);
        CorrectionDiff.Apply("Land.EastAdjacentArea", EastAdjacentArea, edit.EastAdjacentArea, v => EastAdjacentArea = v, diff);
        CorrectionDiff.Apply("Land.EastBoundaryLength", EastBoundaryLength, edit.EastBoundaryLength, v => EastBoundaryLength = v, diff);
        CorrectionDiff.Apply("Land.WestAdjacentArea", WestAdjacentArea, edit.WestAdjacentArea, v => WestAdjacentArea = v, diff);
        CorrectionDiff.Apply("Land.WestBoundaryLength", WestBoundaryLength, edit.WestBoundaryLength, v => WestBoundaryLength = v, diff);
        CorrectionDiff.Apply("Land.PondArea", PondArea, edit.PondArea, v => PondArea = v, diff);
        CorrectionDiff.Apply("Land.PondDepth", PondDepth, edit.PondDepth, v => PondDepth = v, diff);
        CorrectionDiff.Apply("Land.HasBuilding", HasBuilding, edit.HasBuilding, v => HasBuilding = v, diff);
        CorrectionDiff.Apply("Land.HasBuildingOther", HasBuildingOther, edit.HasBuildingOther, v => HasBuildingOther = v, diff);
        CorrectionDiff.Apply("Land.Remark", Remark, edit.Remark, v => Remark = v, diff);
        CorrectionDiff.Apply("Land.IsRentedOut", IsRentedOut, edit.IsRentedOut, v => IsRentedOut = v, diff);
    }

    /// <summary>
    /// Applies corrections to existing titles, matched by id. Titles are corrected in place — never
    /// removed and re-added — so the ids referenced by the audit trail stay stable.
    /// </summary>
    /// <exception cref="Shared.Exceptions.NotFoundException">
    /// A supplied TitleId does not belong to this land detail. Not ignored: it means the caller sent
    /// a correction for the wrong property, and silently dropping it would leave the admin believing
    /// the edit was saved.
    /// </exception>
    internal void ApplyTitleCorrections(
        IReadOnlyList<LandTitleCorrection> edits,
        Dictionary<string, object?> diff)
    {
        foreach (var edit in edits)
        {
            var title = _titles.FirstOrDefault(t => t.Id == edit.TitleId)
                        ?? throw new NotFoundException("LandTitle", edit.TitleId);

            title.ApplyCorrection(edit, diff);
        }
    }
}
