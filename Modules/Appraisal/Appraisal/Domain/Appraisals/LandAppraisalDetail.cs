using System.Data.SqlTypes;

namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Land property appraisal details including location, access, utilities, legal restrictions, and boundaries.
/// 1:1 relationship with AppraisalProperty (PropertyType = Land)
/// Naming aligned with LandAndBuildingAppraisalDetail for consistency.
/// </summary>
public class LandAppraisalDetail : Entity<Guid>
{
    private readonly List<LandTitle> _titles = [];
    // SequenceNumber, then Id compared the way SQL Server orders uniqueidentifier (SqlGuid), so rows that
    // predate the column (all 0) come out in the same order as every SQL reader's `ORDER BY SequenceNumber, Id`
    // whatever order EF loaded them in — Guid.CompareTo orders the bytes differently.
    public IReadOnlyList<LandTitle> Titles =>
        _titles.OrderBy(t => t.SequenceNumber).ThenBy(t => new SqlGuid(t.Id)).ToList().AsReadOnly();

    private readonly List<LandAreaDeduction> _deductions = [];
    public IReadOnlyList<LandAreaDeduction> Deductions => _deductions.AsReadOnly();

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
    // The original three encroachment fields. Descriptive only — they were never deducted from
    // anything, and they still are not: the money-bearing figure is DeductedAreaInSqWa below, backed
    // by the Deductions rows. Kept as-is so old records, the 360 view and the data-correction
    // whitelist keep working unchanged.
    public bool? IsEncroached { get; private set; }
    public string? EncroachmentRemark { get; private set; }

    /// <summary>
    /// Sum of <see cref="Deductions"/>, maintained by the domain — never set from the outside.
    /// Stored so the read-side SQL can subtract one column; see <c>RecalculateDeductedArea</c>.
    /// </summary>
    public decimal? DeductedAreaInSqWa { get; private set; }
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

    /// <summary>
    /// Registered area across all title deeds — the legal fact. Report this to Collateral Master,
    /// the AS400 exports and the per-title rows of the book: encroachment is an appraisal judgement
    /// and does not change how big the parcel legally is.
    /// </summary>
    public decimal TotalLandAreaInSqWa =>
        _titles.Where(t => t.Area != null && t.Area.HasValue)
               .Sum(t => t.Area!.TotalSquareWa ?? 0);

    /// <summary>
    /// The area an appraisal may actually price: registered area less everything the appraiser
    /// listed in <see cref="Deductions"/>. THIS is what every pricing path must use — it reaches
    /// them through PricingPropertyDataService, which is the only place that has to choose.
    /// <para>
    /// Floored at zero: a deduction typed larger than the parcel is a data-entry slip, and a
    /// negative area would otherwise flow straight into an area × rate multiplication.
    /// </para>
    /// </summary>
    public decimal NetLandAreaInSqWa =>
        Math.Max(0m, TotalLandAreaInSqWa - (DeductedAreaInSqWa ?? 0m));

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
            copy.AddTitle(titleCopy); // numbered 1..n in the source's order
        }

        foreach (var deduction in source.Deductions)
        {
            var deductionCopy = LandAreaDeduction.Create(copy.Id, deduction.ReasonCode);
            deductionCopy.Update(deduction.ReasonOther, deduction.AreaInSqWa, deduction.Remark);
            copy._deductions.Add(deductionCopy);
        }

        // Sum only: a copy carries the source as it stands, even rows saved before the deed guard
        // below existed — blocking the copy would block the workflow that makes it.
        copy.SumDeductions();

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
        // A title added without a position goes last, so a path that does not number it cannot become
        // "the first title" that LOS / AS400 / reports pick.
        if (title.SequenceNumber == 0)
            title.SetSequenceNumber(_titles.Count == 0 ? 1 : _titles.Max(t => t.SequenceNumber) + 1);
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

    public void AddDeduction(LandAreaDeduction deduction)
    {
        _deductions.Add(deduction);
        SumDeductions();
    }

    public void RemoveDeduction(Guid deductionId)
    {
        var deduction = _deductions.FirstOrDefault(d => d.Id == deductionId);
        if (deduction != null) _deductions.Remove(deduction);
        SumDeductions();
    }

    public void UpdateDeduction(LandAreaDeduction updatedDeduction)
    {
        var deduction = _deductions.FirstOrDefault(d => d.Id == updatedDeduction.Id);
        if (deduction != null)
        {
            deduction.ChangeReason(updatedDeduction.ReasonCode);
            deduction.Update(
                updatedDeduction.ReasonOther,
                updatedDeduction.AreaInSqWa,
                updatedDeduction.Remark);
        }

        SumDeductions();
    }

    /// <summary>
    /// Settles <see cref="DeductedAreaInSqWa"/> once every title and deduction is in place, and
    /// refuses deductions that add up to more than the registered area. Call it LAST, after both
    /// lists are synced: the update handlers edit rows in place, so the list is only consistent
    /// once the whole sync has run. Idempotent: calling it twice changes nothing.
    /// <para>
    /// Only when the deed area is fully known: every title carries an area. Until then the
    /// registered total is partial, and a half-filled form must still save.
    /// </para>
    /// </summary>
    public void RecalculateDeductedArea()
    {
        SumDeductions();

        // A 0-0-0 area is a form field left at its default, not a deed with no land.
        if (_titles.Count == 0 || _titles.Exists(t => t.Area?.TotalSquareWa is not > 0m))
            return;

        // Compare what will be STORED, or a list that passes now could fail on the next save once it
        // comes back from the database: each title column is decimal(10,2), rounded one column at a
        // time, and each deduction decimal(18,4).
        static decimal Stored(decimal? value, int scale) =>
            Math.Round(value ?? 0m, scale, MidpointRounding.AwayFromZero);
        var deedArea = _titles.Sum(t =>
            Stored(t.Area!.Rai, 2) * 400m + Stored(t.Area.Ngan, 2) * 100m + Stored(t.Area.SquareWa, 2));
        var deducted = _deductions.Sum(d => Stored(d.AreaInSqWa, 4));
        if (Stored(deducted, 2) > deedArea)
            throw new DomainException(
                $"Land area deductions ({DeductedAreaInSqWa:#,##0.##} sq wa) exceed the registered title area ({deedArea:#,##0.##} sq wa).");
    }

    /// <summary>
    /// Keeps <see cref="DeductedAreaInSqWa"/> equal to the rows behind it. Stored rather than
    /// computed so the read-side SQL twin in <c>PricingPropertyDataService.LandAreaSql</c> can
    /// subtract a single column instead of aggregating a child table on every pricing screen load.
    /// No deed guard here: the mutators above run mid-sync, when rows not yet updated still carry
    /// their old areas — <see cref="RecalculateDeductedArea"/> checks the finished state.
    /// </summary>
    private void SumDeductions()
    {
        DeductedAreaInSqWa = _deductions.Sum(d => d.AreaInSqWa ?? 0m);
    }
}
