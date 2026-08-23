namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Condominium property appraisal details including location, materials, and facilities.
/// 1:1 relationship with AppraisalProperty (PropertyType = Condo)
/// </summary>
public class CondoAppraisalDetail : Entity<Guid>
{
    // Foreign Key - 1:1 with AppraisalProperties
    public Guid AppraisalPropertyId { get; private set; }

    // Property Identification
    public string? PropertyName { get; private set; }
    public string? CondoName { get; private set; }
    public string? BuildingNumber { get; private set; }
    public string? ModelName { get; private set; }
    public string? BuiltOnTitleNumber { get; private set; }
    public string? CondoRegistrationNumber { get; private set; }
    public string? RoomNumber { get; private set; }
    public string? FloorNumber { get; private set; }
    public int? PhysicalFloorNumber { get; private set; }
    public decimal? UsableArea { get; private set; }
    public decimal? ConstructionCompletionPercent { get; private set; }

    // Unit deed identifiers (required for Collateral master dedup key)
    public string? TitleNumber { get; private set; }
    public string? TitleType { get; private set; }

    // GPS Coordinates (Value Object)
    public GpsCoordinate? Coordinates { get; private set; }

    // Administrative Address (Value Object)
    public Address? Address { get; private set; }
    public string? LandOffice { get; private set; }

    // Dopa Address (Value Object)
    public Address? DopaAddress { get; private set; }

    // Owner
    public string? OwnerName { get; private set; }
    public bool? IsOwnerVerified { get; private set; }
    public string? BuildingConditionType { get; private set; }
    public string? BuildingConditionTypeOther { get; private set; }
    public string? HasObligation { get; private set; }
    public string? ObligationDetails { get; private set; }
    public string? DocumentValidationResultType { get; private set; }

    // Location Details
    public string? LocationType { get; private set; }
    public string? Street { get; private set; }
    public string? Soi { get; private set; }
    public decimal? DistanceFromMainRoad { get; private set; }
    public decimal? AccessRoadWidth { get; private set; }
    public short? RightOfWay { get; private set; }
    public string? RoadSurfaceType { get; private set; }
    public string? RoadSurfaceTypeOther { get; private set; }
    public List<string>? PublicUtilityType { get; private set; }
    public string? PublicUtilityTypeOther { get; private set; }
    public List<string>? LandEntranceExitType { get; private set; }
    public string? LandEntranceExitTypeOther { get; private set; }

    // Land Characteristics (underlying land attributes for the condo unit)
    public string? LandFillType { get; private set; }
    public string? LandFillTypeOther { get; private set; }
    public string? UrbanPlanningType { get; private set; }
    public List<string>? LandUseType { get; private set; }
    public string? LandUseTypeOther { get; private set; }

    // Building Info
    public string? DecorationType { get; private set; }
    public string? DecorationTypeOther { get; private set; }
    public int? BuildingAge { get; private set; }
    public int? ConstructionYear { get; private set; }
    public decimal? NumberOfFloors { get; private set; }
    public string? BuildingFormType { get; private set; }
    public string? ConstructionMaterialType { get; private set; }

    // Layout & Materials
    public string? RoomLayoutType { get; private set; }
    public string? RoomLayoutTypeOther { get; private set; }
    public List<string>? LocationViewType { get; private set; }
    public string? LocationViewTypeOther { get; private set; }
    public string? GroundFloorMaterialType { get; private set; }
    public string? GroundFloorMaterialTypeOther { get; private set; }
    public string? UpperFloorMaterialType { get; private set; }
    public string? UpperFloorMaterialTypeOther { get; private set; }
    public string? BathroomFloorMaterialType { get; private set; }
    public string? BathroomFloorMaterialTypeOther { get; private set; }
    public List<string>? RoofType { get; private set; }
    public string? RoofTypeOther { get; private set; }

    // Area
    private readonly List<CondoAppraisalAreaDetail> _areaDetails = [];
    public IReadOnlyList<CondoAppraisalAreaDetail> AreaDetails => _areaDetails.AsReadOnly();
    public decimal? TotalBuildingArea { get; private set; }

    // Legal Restrictions
    public bool? IsExpropriated { get; private set; }
    public string? ExpropriationRemark { get; private set; }
    public bool? IsInExpropriationLine { get; private set; }
    public string? ExpropriationLineRemark { get; private set; }
    public string? RoyalDecree { get; private set; }
    public bool? IsForestBoundary { get; private set; }
    public string? ForestBoundaryRemark { get; private set; }

    // Facilities & Environment
    public List<string>? FacilityType { get; private set; }
    public string? FacilityTypeOther { get; private set; }
    public List<string>? EnvironmentType { get; private set; }
    public string? EnvironmentTypeOther { get; private set; }

    // Pricing
    public bool? IsMissingFromSurvey { get; private set; }
    public decimal? GovernmentPricePerSqm { get; private set; }
    public decimal? GovernmentPrice { get; private set; }
    // Fire-insurance condition selected by the appraiser (matches Parameter module's
    // 'FireInsuranceCondition' group); BuildingInsurancePrice is derived from it server-side
    // (RatePerSqm × UsableArea) rather than authored directly by the client.
    public string? FireInsuranceCondition { get; private set; }
    public decimal? BuildingInsurancePrice { get; private set; }
    public decimal? SellingPrice { get; private set; }
    public decimal? ForcedSalePrice { get; private set; }

    // Other
    public string? Remark { get; private set; }

    private CondoAppraisalDetail()
    {
        // For EF Core
    }

    public static CondoAppraisalDetail Create(Guid appraisalPropertyId)
    {
        return new CondoAppraisalDetail
        {
            AppraisalPropertyId = appraisalPropertyId
        };
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public void Update(
        // Property Identification
        string? propertyName = null,
        string? condoName = null,
        string? buildingNumber = null,
        string? modelName = null,
        string? condoRegistrationNumber = null,
        string? roomNumber = null,
        string? floorNumber = null,
        decimal? usableArea = null,
        decimal? constructionCompletionPercent = null,
        string? titleNumber = null,
        string? titleType = null,
        // Value Objects
        GpsCoordinate? coordinates = null,
        Address? address = null,
        // Owner
        string? ownerName = null,
        bool? isOwnerVerified = null,
        string? buildingConditionType = null,
        string? buildingConditionTypeOther = null,
        string? hasObligation = null,
        string? obligationDetails = null,
        string? documentValidationResultType = null,
        // Location Details
        string? locationType = null,
        string? street = null,
        string? soi = null,
        decimal? distanceFromMainRoad = null,
        decimal? accessRoadWidth = null,
        short? rightOfWay = null,
        string? roadSurfaceType = null,
        string? roadSurfaceTypeOther = null,
        List<string>? publicUtilityType = null,
        string? publicUtilityTypeOther = null,
        // Building Info
        string? decorationType = null,
        string? decorationTypeOther = null,
        int? buildingAge = null,
        int? constructionYear = null,
        decimal? numberOfFloors = null,
        string? buildingFormType = null,
        string? constructionMaterialType = null,
        // Layout & Materials
        string? roomLayoutType = null,
        string? roomLayoutTypeOther = null,
        List<string>? locationViewType = null,
        string? locationViewTypeOther = null,
        string? groundFloorMaterialType = null,
        string? groundFloorMaterialTypeOther = null,
        string? upperFloorMaterialType = null,
        string? upperFloorMaterialTypeOther = null,
        string? bathroomFloorMaterialType = null,
        string? bathroomFloorMaterialTypeOther = null,
        List<string>? roofType = null,
        string? roofTypeOther = null,
        // Area
        decimal? totalBuildingArea = null,
        // Legal Restrictions
        bool? isExpropriated = null,
        string? expropriationRemark = null,
        bool? isInExpropriationLine = null,
        string? expropriationLineRemark = null,
        string? royalDecree = null,
        bool? isForestBoundary = null,
        string? forestBoundaryRemark = null,
        // Facilities & Environment
        List<string>? facilityType = null,
        string? facilityTypeOther = null,
        List<string>? environmentType = null,
        string? environmentTypeOther = null,
        // Pricing
        decimal? buildingInsurancePrice = null,
        decimal? sellingPrice = null,
        decimal? forcedSalePrice = null,
        // Other
        string? remark = null,
        // Scalar field
        string? landOffice = null,
        // DOPA address
        Address? dopaAddress = null,
        // Land Characteristics (appended — see Update() ordering note)
        List<string>? landEntranceExitType = null,
        string? landEntranceExitTypeOther = null,
        string? landFillType = null,
        string? landFillTypeOther = null,
        string? urbanPlanningType = null,
        List<string>? landUseType = null,
        string? landUseTypeOther = null,
        // Government Price
        bool? isMissingFromSurvey = null,
        decimal? governmentPricePerSqm = null,
        decimal? governmentPrice = null,
        // Fire Insurance (appended — see Update() ordering note)
        string? fireInsuranceCondition = null)
    {
        // Property Identification
        PropertyName = propertyName;
        CondoName = condoName;
        BuildingNumber = buildingNumber;
        ModelName = modelName;
        CondoRegistrationNumber = condoRegistrationNumber;
        RoomNumber = roomNumber;
        FloorNumber = floorNumber;
        UsableArea = usableArea;
        ConstructionCompletionPercent = constructionCompletionPercent;
        TitleNumber = titleNumber;
        TitleType = titleType;

        // Value Objects
        Coordinates = coordinates;
        Address = address;

        // Owner
        OwnerName = ownerName;
        IsOwnerVerified = isOwnerVerified;
        BuildingConditionType = buildingConditionType;
        BuildingConditionTypeOther = buildingConditionTypeOther;
        HasObligation = hasObligation;
        ObligationDetails = obligationDetails;
        DocumentValidationResultType = documentValidationResultType;

        // Location Details
        LocationType = locationType;
        Street = street;
        Soi = soi;
        DistanceFromMainRoad = distanceFromMainRoad;
        AccessRoadWidth = accessRoadWidth;
        RightOfWay = rightOfWay;
        RoadSurfaceType = roadSurfaceType;
        RoadSurfaceTypeOther = roadSurfaceTypeOther;
        PublicUtilityType = publicUtilityType;
        PublicUtilityTypeOther = publicUtilityTypeOther;

        // Building Info
        DecorationType = decorationType;
        DecorationTypeOther = decorationTypeOther;
        BuildingAge = buildingAge;
        ConstructionYear = constructionYear;
        NumberOfFloors = numberOfFloors;
        BuildingFormType = buildingFormType;
        ConstructionMaterialType = constructionMaterialType;

        // Layout & Materials
        RoomLayoutType = roomLayoutType;
        RoomLayoutTypeOther = roomLayoutTypeOther;
        LocationViewType = locationViewType;
        LocationViewTypeOther = locationViewTypeOther;
        GroundFloorMaterialType = groundFloorMaterialType;
        GroundFloorMaterialTypeOther = groundFloorMaterialTypeOther;
        UpperFloorMaterialType = upperFloorMaterialType;
        UpperFloorMaterialTypeOther = upperFloorMaterialTypeOther;
        BathroomFloorMaterialType = bathroomFloorMaterialType;
        BathroomFloorMaterialTypeOther = bathroomFloorMaterialTypeOther;
        RoofType = roofType;
        RoofTypeOther = roofTypeOther;

        // Area
        TotalBuildingArea = totalBuildingArea;

        // Legal Restrictions
        IsExpropriated = isExpropriated;
        ExpropriationRemark = expropriationRemark;
        IsInExpropriationLine = isInExpropriationLine;
        ExpropriationLineRemark = expropriationLineRemark;
        RoyalDecree = royalDecree;
        IsForestBoundary = isForestBoundary;
        ForestBoundaryRemark = forestBoundaryRemark;

        // Facilities & Environment
        FacilityType = facilityType;
        FacilityTypeOther = facilityTypeOther;
        EnvironmentType = environmentType;
        EnvironmentTypeOther = environmentTypeOther;

        // Pricing
        BuildingInsurancePrice = buildingInsurancePrice;
        SellingPrice = sellingPrice;
        ForcedSalePrice = forcedSalePrice;

        // Other
        Remark = remark;

        // Address scalar + Dopa
        LandOffice = landOffice;
        DopaAddress = dopaAddress;

        // Land Characteristics
        LandEntranceExitType = landEntranceExitType;
        LandEntranceExitTypeOther = landEntranceExitTypeOther;
        LandFillType = landFillType;
        LandFillTypeOther = landFillTypeOther;
        UrbanPlanningType = urbanPlanningType;
        LandUseType = landUseType;
        LandUseTypeOther = landUseTypeOther;

        // Government Price
        IsMissingFromSurvey = isMissingFromSurvey;
        GovernmentPricePerSqm = governmentPricePerSqm;
        GovernmentPrice = governmentPrice;

        // Fire Insurance
        FireInsuranceCondition = fireInsuranceCondition;
    }

    /// <summary>
    /// Narrow update for the PMA save path — touches ONLY the fields the PMA form authors.
    /// <para>
    /// Deliberately NOT <see cref="Update"/>: that method is a full overwrite of every property,
    /// so calling it from PMA (which supplies a handful of arguments) silently reset the ~60
    /// unsupplied fields to null — wiping appraiser-entered condo detail, land attributes and
    /// government price. Keep this method narrow; do not grow it into a second full overwrite.
    /// </para>
    /// </summary>
    public void UpdatePmaFields(
        string? condoName = null,
        string? ownerName = null,
        string? buildingNumber = null,
        string? titleNumber = null,
        string? condoRegistrationNumber = null,
        string? roomNumber = null,
        string? floorNumber = null,
        Address? address = null)
    {
        CondoName = condoName;
        OwnerName = ownerName;
        BuildingNumber = buildingNumber;
        TitleNumber = titleNumber;
        CondoRegistrationNumber = condoRegistrationNumber;
        RoomNumber = roomNumber;
        FloorNumber = floorNumber;
        Address = address;
    }


    public static CondoAppraisalDetail CopyFrom(CondoAppraisalDetail source, Guid newPropertyId)
    {
        var copy = new CondoAppraisalDetail
        {
            AppraisalPropertyId = newPropertyId,
            PropertyName = source.PropertyName,
            CondoName = source.CondoName,
            BuildingNumber = source.BuildingNumber,
            ModelName = source.ModelName,
            CondoRegistrationNumber = source.CondoRegistrationNumber,
            RoomNumber = source.RoomNumber,
            FloorNumber = source.FloorNumber,
            PhysicalFloorNumber = source.PhysicalFloorNumber,
            UsableArea = source.UsableArea,
            ConstructionCompletionPercent = source.ConstructionCompletionPercent,
            TitleNumber = source.TitleNumber,
            TitleType = source.TitleType,
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
            BuildingConditionType = source.BuildingConditionType,
            BuildingConditionTypeOther = source.BuildingConditionTypeOther,
            HasObligation = source.HasObligation,
            ObligationDetails = source.ObligationDetails,
            DocumentValidationResultType = source.DocumentValidationResultType,
            LocationType = source.LocationType,
            Street = source.Street,
            Soi = source.Soi,
            DistanceFromMainRoad = source.DistanceFromMainRoad,
            AccessRoadWidth = source.AccessRoadWidth,
            RightOfWay = source.RightOfWay,
            RoadSurfaceType = source.RoadSurfaceType,
            RoadSurfaceTypeOther = source.RoadSurfaceTypeOther,
            PublicUtilityType = source.PublicUtilityType?.ToList(),
            PublicUtilityTypeOther = source.PublicUtilityTypeOther,
            LandEntranceExitType = source.LandEntranceExitType?.ToList(),
            LandEntranceExitTypeOther = source.LandEntranceExitTypeOther,
            LandFillType = source.LandFillType,
            LandFillTypeOther = source.LandFillTypeOther,
            UrbanPlanningType = source.UrbanPlanningType,
            LandUseType = source.LandUseType?.ToList(),
            LandUseTypeOther = source.LandUseTypeOther,
            DecorationType = source.DecorationType,
            DecorationTypeOther = source.DecorationTypeOther,
            BuildingAge = source.BuildingAge,
            ConstructionYear = source.ConstructionYear,
            NumberOfFloors = source.NumberOfFloors,
            BuildingFormType = source.BuildingFormType,
            ConstructionMaterialType = source.ConstructionMaterialType,
            RoomLayoutType = source.RoomLayoutType,
            RoomLayoutTypeOther = source.RoomLayoutTypeOther,
            LocationViewType = source.LocationViewType?.ToList(),
            LocationViewTypeOther = source.LocationViewTypeOther,
            GroundFloorMaterialType = source.GroundFloorMaterialType,
            GroundFloorMaterialTypeOther = source.GroundFloorMaterialTypeOther,
            UpperFloorMaterialType = source.UpperFloorMaterialType,
            UpperFloorMaterialTypeOther = source.UpperFloorMaterialTypeOther,
            BathroomFloorMaterialType = source.BathroomFloorMaterialType,
            BathroomFloorMaterialTypeOther = source.BathroomFloorMaterialTypeOther,
            RoofType = source.RoofType?.ToList(),
            RoofTypeOther = source.RoofTypeOther,
            TotalBuildingArea = source.TotalBuildingArea,
            IsExpropriated = source.IsExpropriated,
            ExpropriationRemark = source.ExpropriationRemark,
            IsInExpropriationLine = source.IsInExpropriationLine,
            ExpropriationLineRemark = source.ExpropriationLineRemark,
            RoyalDecree = source.RoyalDecree,
            IsForestBoundary = source.IsForestBoundary,
            ForestBoundaryRemark = source.ForestBoundaryRemark,
            FacilityType = source.FacilityType?.ToList(),
            FacilityTypeOther = source.FacilityTypeOther,
            EnvironmentType = source.EnvironmentType?.ToList(),
            EnvironmentTypeOther = source.EnvironmentTypeOther,
            IsMissingFromSurvey = source.IsMissingFromSurvey,
            GovernmentPricePerSqm = source.GovernmentPricePerSqm,
            GovernmentPrice = source.GovernmentPrice,
            FireInsuranceCondition = source.FireInsuranceCondition,
            BuildingInsurancePrice = source.BuildingInsurancePrice,
            SellingPrice = source.SellingPrice,
            ForcedSalePrice = source.ForcedSalePrice,
            Remark = source.Remark
        };

        foreach (var area in source.AreaDetails)
        {
            var areaCopy = CondoAppraisalAreaDetail.Create(area.Sequence ,area.AreaDescription, area.AreaSize);
            copy._areaDetails.Add(areaCopy);
        }

        return copy;
    }

    public void AddCondoAreaDetail(CondoAppraisalAreaDetail areaDetails)
    {
        _areaDetails.Add(areaDetails);
    }

    public void RemoveCondoAreaDetail(Guid areaDetailId)
    {
        var item = _areaDetails.FirstOrDefault(a => a.Id == areaDetailId);
        if (item != null) _areaDetails.Remove(item);
    }

    /// <summary>
    /// Applies admin corrections to this unit, recording each change in <paramref name="diff"/>.
    /// Not routed through <see cref="Update"/>, which overwrites every property unconditionally.
    ///
    /// Note <see cref="PhysicalFloorNumber"/>: it had no mutator anywhere in the domain before this
    /// method, so a wrong surveyed floor was previously uncorrectable.
    /// </summary>
    internal void ApplyCorrection(CondoCorrection edit, Dictionary<string, object?> diff)
    {
        CorrectionDiff.Apply("Condo.PropertyName", PropertyName, edit.PropertyName, v => PropertyName = v, diff);
        CorrectionDiff.Apply("Condo.CondoName", CondoName, edit.CondoName, v => CondoName = v, diff);
        CorrectionDiff.Apply("Condo.BuildingNumber", BuildingNumber, edit.BuildingNumber, v => BuildingNumber = v, diff);
        CorrectionDiff.Apply("Condo.ModelName", ModelName, edit.ModelName, v => ModelName = v, diff);
        CorrectionDiff.Apply("Condo.BuiltOnTitleNumber", BuiltOnTitleNumber, edit.BuiltOnTitleNumber, v => BuiltOnTitleNumber = v, diff);
        CorrectionDiff.Apply("Condo.CondoRegistrationNumber", CondoRegistrationNumber, edit.CondoRegistrationNumber, v => CondoRegistrationNumber = v, diff);
        CorrectionDiff.Apply("Condo.RoomNumber", RoomNumber, edit.RoomNumber, v => RoomNumber = v, diff);
        CorrectionDiff.Apply("Condo.FloorNumber", FloorNumber, edit.FloorNumber, v => FloorNumber = v, diff);
        CorrectionDiff.Apply("Condo.PhysicalFloorNumber", PhysicalFloorNumber, edit.PhysicalFloorNumber, v => PhysicalFloorNumber = v, diff);
        // UsableArea and TotalBuildingArea are deliberately NOT correctable: the government
        // price below is computed from the usable area, and this feature corrects descriptive
        // data only — it neither recomputes prices nor returns the appraisal to the workflow.
        // Same reasoning as the land title and building areas; see CorrectionDto_DoesNotExposeArea.
        CorrectionDiff.Apply("Condo.ConstructionCompletionPercent", ConstructionCompletionPercent, edit.ConstructionCompletionPercent, v => ConstructionCompletionPercent = v, diff);
        CorrectionDiff.Apply("Condo.TitleNumber", TitleNumber, edit.TitleNumber, v => TitleNumber = v, diff);
        CorrectionDiff.Apply("Condo.TitleType", TitleType, edit.TitleType, v => TitleType = v, diff);
        // Coordinates is an immutable record — compare components, rebuild once.
        var latitude = Coordinates?.Latitude;
        var longitude = Coordinates?.Longitude;
        var coordinatesChanged = false;
        CorrectionDiff.Apply("Condo.Latitude", latitude, edit.Latitude,
            v => { latitude = v; coordinatesChanged = true; }, diff);
        CorrectionDiff.Apply("Condo.Longitude", longitude, edit.Longitude,
            v => { longitude = v; coordinatesChanged = true; }, diff);
        if (coordinatesChanged)
            Coordinates = Domain.Appraisals.GpsCoordinate.Create(latitude, longitude);

        // Address is an immutable record — compare components, rebuild once.
        var subDistrict = Address?.SubDistrict;
        var district = Address?.District;
        var province = Address?.Province;
        var addressChanged = false;
        CorrectionDiff.Apply("Condo.SubDistrict", subDistrict, edit.SubDistrict,
            v => { subDistrict = v; addressChanged = true; }, diff);
        CorrectionDiff.Apply("Condo.District", district, edit.District,
            v => { district = v; addressChanged = true; }, diff);
        CorrectionDiff.Apply("Condo.Province", province, edit.Province,
            v => { province = v; addressChanged = true; }, diff);
        if (addressChanged)
            Address = Domain.Appraisals.Address.Create(subDistrict, district, province);

        CorrectionDiff.Apply("Condo.LandOffice", LandOffice, edit.LandOffice, v => LandOffice = v, diff);
        // DopaAddress is an immutable record — compare components, rebuild once.
        var dopaSubDistrict = DopaAddress?.SubDistrict;
        var dopaDistrict = DopaAddress?.District;
        var dopaProvince = DopaAddress?.Province;
        var dopaAddressChanged = false;
        CorrectionDiff.Apply("Condo.DopaSubDistrict", dopaSubDistrict, edit.DopaSubDistrict,
            v => { dopaSubDistrict = v; dopaAddressChanged = true; }, diff);
        CorrectionDiff.Apply("Condo.DopaDistrict", dopaDistrict, edit.DopaDistrict,
            v => { dopaDistrict = v; dopaAddressChanged = true; }, diff);
        CorrectionDiff.Apply("Condo.DopaProvince", dopaProvince, edit.DopaProvince,
            v => { dopaProvince = v; dopaAddressChanged = true; }, diff);
        if (dopaAddressChanged)
            DopaAddress = Domain.Appraisals.Address.Create(dopaSubDistrict, dopaDistrict, dopaProvince);

        CorrectionDiff.Apply("Condo.OwnerName", OwnerName, edit.OwnerName, v => OwnerName = v, diff);
        CorrectionDiff.Apply("Condo.IsOwnerVerified", IsOwnerVerified, edit.IsOwnerVerified, v => IsOwnerVerified = v, diff);
        CorrectionDiff.Apply("Condo.BuildingConditionType", BuildingConditionType, edit.BuildingConditionType, v => BuildingConditionType = v, diff);
        CorrectionDiff.Apply("Condo.BuildingConditionTypeOther", BuildingConditionTypeOther, edit.BuildingConditionTypeOther, v => BuildingConditionTypeOther = v, diff);
        CorrectionDiff.Apply("Condo.HasObligation", HasObligation, edit.HasObligation, v => HasObligation = v, diff);
        CorrectionDiff.Apply("Condo.ObligationDetails", ObligationDetails, edit.ObligationDetails, v => ObligationDetails = v, diff);
        CorrectionDiff.Apply("Condo.DocumentValidationResultType", DocumentValidationResultType, edit.DocumentValidationResultType, v => DocumentValidationResultType = v, diff);
        CorrectionDiff.Apply("Condo.LocationType", LocationType, edit.LocationType, v => LocationType = v, diff);
        CorrectionDiff.Apply("Condo.Street", Street, edit.Street, v => Street = v, diff);
        CorrectionDiff.Apply("Condo.Soi", Soi, edit.Soi, v => Soi = v, diff);
        CorrectionDiff.Apply("Condo.DistanceFromMainRoad", DistanceFromMainRoad, edit.DistanceFromMainRoad, v => DistanceFromMainRoad = v, diff);
        CorrectionDiff.Apply("Condo.AccessRoadWidth", AccessRoadWidth, edit.AccessRoadWidth, v => AccessRoadWidth = v, diff);
        CorrectionDiff.Apply("Condo.RightOfWay", RightOfWay, edit.RightOfWay, v => RightOfWay = v, diff);
        CorrectionDiff.Apply("Condo.RoadSurfaceType", RoadSurfaceType, edit.RoadSurfaceType, v => RoadSurfaceType = v, diff);
        CorrectionDiff.Apply("Condo.RoadSurfaceTypeOther", RoadSurfaceTypeOther, edit.RoadSurfaceTypeOther, v => RoadSurfaceTypeOther = v, diff);
        CorrectionDiff.ApplyList("Condo.PublicUtilityType", PublicUtilityType, edit.PublicUtilityType, v => PublicUtilityType = v, diff);
        CorrectionDiff.Apply("Condo.PublicUtilityTypeOther", PublicUtilityTypeOther, edit.PublicUtilityTypeOther, v => PublicUtilityTypeOther = v, diff);
        CorrectionDiff.ApplyList("Condo.LandEntranceExitType", LandEntranceExitType, edit.LandEntranceExitType, v => LandEntranceExitType = v, diff);
        CorrectionDiff.Apply("Condo.LandEntranceExitTypeOther", LandEntranceExitTypeOther, edit.LandEntranceExitTypeOther, v => LandEntranceExitTypeOther = v, diff);
        CorrectionDiff.Apply("Condo.LandFillType", LandFillType, edit.LandFillType, v => LandFillType = v, diff);
        CorrectionDiff.Apply("Condo.LandFillTypeOther", LandFillTypeOther, edit.LandFillTypeOther, v => LandFillTypeOther = v, diff);
        CorrectionDiff.Apply("Condo.UrbanPlanningType", UrbanPlanningType, edit.UrbanPlanningType, v => UrbanPlanningType = v, diff);
        CorrectionDiff.ApplyList("Condo.LandUseType", LandUseType, edit.LandUseType, v => LandUseType = v, diff);
        CorrectionDiff.Apply("Condo.LandUseTypeOther", LandUseTypeOther, edit.LandUseTypeOther, v => LandUseTypeOther = v, diff);
        CorrectionDiff.Apply("Condo.DecorationType", DecorationType, edit.DecorationType, v => DecorationType = v, diff);
        CorrectionDiff.Apply("Condo.DecorationTypeOther", DecorationTypeOther, edit.DecorationTypeOther, v => DecorationTypeOther = v, diff);
        CorrectionDiff.Apply("Condo.BuildingAge", BuildingAge, edit.BuildingAge, v => BuildingAge = v, diff);
        CorrectionDiff.Apply("Condo.ConstructionYear", ConstructionYear, edit.ConstructionYear, v => ConstructionYear = v, diff);
        CorrectionDiff.Apply("Condo.NumberOfFloors", NumberOfFloors, edit.NumberOfFloors, v => NumberOfFloors = v, diff);
        CorrectionDiff.Apply("Condo.BuildingFormType", BuildingFormType, edit.BuildingFormType, v => BuildingFormType = v, diff);
        CorrectionDiff.Apply("Condo.ConstructionMaterialType", ConstructionMaterialType, edit.ConstructionMaterialType, v => ConstructionMaterialType = v, diff);
        CorrectionDiff.Apply("Condo.RoomLayoutType", RoomLayoutType, edit.RoomLayoutType, v => RoomLayoutType = v, diff);
        CorrectionDiff.Apply("Condo.RoomLayoutTypeOther", RoomLayoutTypeOther, edit.RoomLayoutTypeOther, v => RoomLayoutTypeOther = v, diff);
        CorrectionDiff.ApplyList("Condo.LocationViewType", LocationViewType, edit.LocationViewType, v => LocationViewType = v, diff);
        CorrectionDiff.Apply("Condo.LocationViewTypeOther", LocationViewTypeOther, edit.LocationViewTypeOther, v => LocationViewTypeOther = v, diff);
        CorrectionDiff.Apply("Condo.GroundFloorMaterialType", GroundFloorMaterialType, edit.GroundFloorMaterialType, v => GroundFloorMaterialType = v, diff);
        CorrectionDiff.Apply("Condo.GroundFloorMaterialTypeOther", GroundFloorMaterialTypeOther, edit.GroundFloorMaterialTypeOther, v => GroundFloorMaterialTypeOther = v, diff);
        CorrectionDiff.Apply("Condo.UpperFloorMaterialType", UpperFloorMaterialType, edit.UpperFloorMaterialType, v => UpperFloorMaterialType = v, diff);
        CorrectionDiff.Apply("Condo.UpperFloorMaterialTypeOther", UpperFloorMaterialTypeOther, edit.UpperFloorMaterialTypeOther, v => UpperFloorMaterialTypeOther = v, diff);
        CorrectionDiff.Apply("Condo.BathroomFloorMaterialType", BathroomFloorMaterialType, edit.BathroomFloorMaterialType, v => BathroomFloorMaterialType = v, diff);
        CorrectionDiff.Apply("Condo.BathroomFloorMaterialTypeOther", BathroomFloorMaterialTypeOther, edit.BathroomFloorMaterialTypeOther, v => BathroomFloorMaterialTypeOther = v, diff);
        CorrectionDiff.ApplyList("Condo.RoofType", RoofType, edit.RoofType, v => RoofType = v, diff);
        CorrectionDiff.Apply("Condo.RoofTypeOther", RoofTypeOther, edit.RoofTypeOther, v => RoofTypeOther = v, diff);
        CorrectionDiff.Apply("Condo.IsExpropriated", IsExpropriated, edit.IsExpropriated, v => IsExpropriated = v, diff);
        CorrectionDiff.Apply("Condo.ExpropriationRemark", ExpropriationRemark, edit.ExpropriationRemark, v => ExpropriationRemark = v, diff);
        CorrectionDiff.Apply("Condo.IsInExpropriationLine", IsInExpropriationLine, edit.IsInExpropriationLine, v => IsInExpropriationLine = v, diff);
        CorrectionDiff.Apply("Condo.ExpropriationLineRemark", ExpropriationLineRemark, edit.ExpropriationLineRemark, v => ExpropriationLineRemark = v, diff);
        CorrectionDiff.Apply("Condo.RoyalDecree", RoyalDecree, edit.RoyalDecree, v => RoyalDecree = v, diff);
        CorrectionDiff.Apply("Condo.IsForestBoundary", IsForestBoundary, edit.IsForestBoundary, v => IsForestBoundary = v, diff);
        CorrectionDiff.Apply("Condo.ForestBoundaryRemark", ForestBoundaryRemark, edit.ForestBoundaryRemark, v => ForestBoundaryRemark = v, diff);
        CorrectionDiff.ApplyList("Condo.FacilityType", FacilityType, edit.FacilityType, v => FacilityType = v, diff);
        CorrectionDiff.Apply("Condo.FacilityTypeOther", FacilityTypeOther, edit.FacilityTypeOther, v => FacilityTypeOther = v, diff);
        CorrectionDiff.ApplyList("Condo.EnvironmentType", EnvironmentType, edit.EnvironmentType, v => EnvironmentType = v, diff);
        CorrectionDiff.Apply("Condo.EnvironmentTypeOther", EnvironmentTypeOther, edit.EnvironmentTypeOther, v => EnvironmentTypeOther = v, diff);
        CorrectionDiff.Apply("Condo.IsMissingFromSurvey", IsMissingFromSurvey, edit.IsMissingFromSurvey, v => IsMissingFromSurvey = v, diff);
        CorrectionDiff.Apply("Condo.GovernmentPricePerSqm", GovernmentPricePerSqm, edit.GovernmentPricePerSqm, v => GovernmentPricePerSqm = v, diff);
        CorrectionDiff.Apply("Condo.GovernmentPrice", GovernmentPrice, edit.GovernmentPrice, v => GovernmentPrice = v, diff);
        CorrectionDiff.Apply("Condo.FireInsuranceCondition", FireInsuranceCondition, edit.FireInsuranceCondition, v => FireInsuranceCondition = v, diff);
        CorrectionDiff.Apply("Condo.BuildingInsurancePrice", BuildingInsurancePrice, edit.BuildingInsurancePrice, v => BuildingInsurancePrice = v, diff);
        CorrectionDiff.Apply("Condo.SellingPrice", SellingPrice, edit.SellingPrice, v => SellingPrice = v, diff);
        CorrectionDiff.Apply("Condo.ForcedSalePrice", ForcedSalePrice, edit.ForcedSalePrice, v => ForcedSalePrice = v, diff);
        CorrectionDiff.Apply("Condo.Remark", Remark, edit.Remark, v => Remark = v, diff);
    }
}
