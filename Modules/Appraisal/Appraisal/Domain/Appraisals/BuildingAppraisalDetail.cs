namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Building property appraisal details including construction, condition, structure, and pricing.
/// 1:1 relationship with AppraisalProperty (PropertyType = Building)
/// Naming aligned with LandAndBuildingAppraisalDetail for consistency.
/// </summary>
public class BuildingAppraisalDetail : Entity<Guid>
{
    // Foreign Key - 1:1 with AppraisalProperties
    public Guid AppraisalPropertyId { get; private set; }

    // Property Identification
    public string? PropertyName { get; private set; }
    public string? BuildingNumber { get; private set; }
    public string? ModelName { get; private set; }
    public string? BuiltOnTitleNumber { get; private set; }
    public string? HouseNumber { get; private set; }
    public string? NoHouseNumber { get; private set; }

    // Owner
    public string? OwnerName { get; private set; }
    public bool? IsOwnerVerified { get; private set; }
    public string? HasObligation { get; private set; }
    public string? ObligationDetails { get; private set; }

    // Building Status
    public string? BuildingConditionType { get; private set; }
    public string? BuildingConditionTypeOther { get; private set; }
    public bool? IsUnderConstruction { get; private set; }
    public DateTime? ConstructionLicenseExpirationDate { get; private set; }
    public bool? IsAppraisable { get; private set; } = true;

    // Building Info
    public string? BuildingType { get; private set; }
    public string? BuildingTypeOther { get; private set; }
    public decimal? NumberOfFloors { get; private set; }
    public string? DecorationType { get; private set; }
    public string? DecorationTypeOther { get; private set; }
    public bool? IsEncroachingOthers { get; private set; }
    public string? EncroachingOthersRemark { get; private set; }
    public decimal? EncroachingOthersArea { get; private set; }

    // Construction Details
    public string? BuildingMaterialType { get; private set; }
    public string? BuildingStyleType { get; private set; }
    public string? BuildingStyleTypeOther { get; private set; }
    public bool? IsResidential { get; private set; }
    public int? BuildingAge { get; private set; }
    public int? ConstructionYear { get; private set; }
    public string? ResidentialRemark { get; private set; }
    public string? ConstructionStyleType { get; private set; }
    public string? ConstructionStyleRemark { get; private set; }

    // Structure Components
    public List<string>? StructureType { get; private set; }
    public string? StructureTypeOther { get; private set; }
    public List<string>? RoofFrameType { get; private set; }
    public string? RoofFrameTypeOther { get; private set; }
    public List<string>? RoofType { get; private set; }
    public string? RoofTypeOther { get; private set; }
    public List<string>? CeilingType { get; private set; }
    public string? CeilingTypeOther { get; private set; }
    public List<string>? InteriorWallType { get; private set; }
    public string? InteriorWallTypeOther { get; private set; }
    public List<string>? ExteriorWallType { get; private set; }
    public string? ExteriorWallTypeOther { get; private set; }
    public List<string>? FenceType { get; private set; }
    public string? FenceTypeOther { get; private set; }
    public string? ConstructionType { get; private set; }
    public string? ConstructionTypeOther { get; private set; }

    // Utilization
    public string? UtilizationType { get; private set; }
    public string? UtilizationTypeOther { get; private set; }

    // Area
    public decimal? TotalBuildingArea { get; private set; }

    // Pricing
    /// <summary>
    /// The Building Cost Value shown on the property screen: the appraiser's typed figure, otherwise the one
    /// computed from every depreciation row (<see cref="ResolveDerivedValues"/>, on every save). Null only
    /// when nothing was typed and there are no rows; readers still fall back to the computed value for it.
    /// </summary>
    public decimal? BuildingCostValue { get; private set; }

    /// <summary>
    /// The fire-insurance coverage shown on the property screen: the appraiser's typed figure, otherwise the
    /// one computed from the IsBuilding depreciation rows (<see cref="ResolveDerivedValues"/>, on every save).
    /// Null only when nothing was typed and there are no IsBuilding rows. A non-null value is not proof it was
    /// typed — the frontend treats a value equal to the computed one as "not entered".
    /// </summary>
    public decimal? BuildingInsurancePrice { get; private set; }

    // Other
    public string? Remark { get; private set; }

    // Depreciation Details (OwnsMany)
    private readonly List<BuildingDepreciationDetail> _depreciationDetails = [];
    public IReadOnlyList<BuildingDepreciationDetail> DepreciationDetails => _depreciationDetails.AsReadOnly();

    // Surfaces (OwnsMany)
    private readonly List<BuildingAppraisalSurface> _surfaces = [];
    public IReadOnlyList<BuildingAppraisalSurface> Surfaces => _surfaces.AsReadOnly();

    private BuildingAppraisalDetail()
    {
        // For EF Core
    }

    public static BuildingAppraisalDetail Create(Guid appraisalPropertyId)
    {
        return new BuildingAppraisalDetail
        {
            AppraisalPropertyId = appraisalPropertyId
        };
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public void Update(
        // Property Identification
        string? propertyName = null,
        string? buildingNumber = null,
        string? modelName = null,
        string? builtOnTitleNumber = null,
        string? houseNumber = null,
        string? noHouseNumber = null,
        // Owner
        string? ownerName = null,
        bool? isOwnerVerified = null,
        string? hasObligation = null,
        string? obligationDetails = null,
        // Building Status
        string? buildingConditionType = null,
        string? buildingConditionTypeOther = null,
        bool? isUnderConstruction = null,
        DateTime? constructionLicenseExpirationDate = null,
        bool? isAppraisable = null,
        // Building Info
        string? buildingType = null,
        string? buildingTypeOther = null,
        decimal? numberOfFloors = null,
        string? decorationType = null,
        string? decorationTypeOther = null,
        bool? isEncroachingOthers = null,
        string? encroachingOthersRemark = null,
        decimal? encroachingOthersArea = null,
        // Construction Details
        string? buildingMaterialType = null,
        string? buildingStyleType = null,
        string? buildingStyleTypeOther = null,
        bool? isResidential = null,
        int? buildingAge = null,
        int? constructionYear = null,
        string? residentialRemark = null,
        string? constructionStyleType = null,
        string? constructionStyleRemark = null,
        // Structure Components
        List<string>? structureType = null,
        string? structureTypeOther = null,
        List<string>? roofFrameType = null,
        string? roofFrameTypeOther = null,
        List<string>? roofType = null,
        string? roofTypeOther = null,
        List<string>? ceilingType = null,
        string? ceilingTypeOther = null,
        List<string>? interiorWallType = null,
        string? interiorWallTypeOther = null,
        List<string>? exteriorWallType = null,
        string? exteriorWallTypeOther = null,
        List<string>? fenceType = null,
        string? fenceTypeOther = null,
        string? constructionType = null,
        string? constructionTypeOther = null,
        // Utilization
        string? utilizationType = null,
        string? utilizationTypeOther = null,
        // Area & Pricing
        decimal? totalBuildingArea = null,
        decimal? buildingCostValue = null,
        decimal? buildingInsurancePrice = null,
        // Other
        string? remark = null)
    {
        // Property Identification
        PropertyName = propertyName;
        BuildingNumber = buildingNumber;
        ModelName = modelName;
        BuiltOnTitleNumber = builtOnTitleNumber;
        HouseNumber = houseNumber;
        NoHouseNumber = noHouseNumber;

        // Owner
        OwnerName = ownerName;
        IsOwnerVerified = isOwnerVerified;
        HasObligation = hasObligation;
        ObligationDetails = obligationDetails;

        // Building Status
        BuildingConditionType = buildingConditionType;
        BuildingConditionTypeOther = buildingConditionTypeOther;
        IsUnderConstruction = isUnderConstruction;

        ConstructionLicenseExpirationDate = constructionLicenseExpirationDate;
        IsAppraisable = isAppraisable;

        // Building Info
        BuildingType = buildingType;
        BuildingTypeOther = buildingTypeOther;
        NumberOfFloors = numberOfFloors;
        DecorationType = decorationType;
        DecorationTypeOther = decorationTypeOther;
        IsEncroachingOthers = isEncroachingOthers;
        EncroachingOthersRemark = encroachingOthersRemark;
        EncroachingOthersArea = encroachingOthersArea;

        // Construction Details
        BuildingMaterialType = buildingMaterialType;
        BuildingStyleType = buildingStyleType;
        BuildingStyleTypeOther = buildingStyleTypeOther;
        IsResidential = isResidential;
        BuildingAge = buildingAge;
        ConstructionYear = constructionYear;
        ResidentialRemark = residentialRemark;
        ConstructionStyleType = constructionStyleType;
        ConstructionStyleRemark = constructionStyleRemark;

        // Structure Components
        StructureType = structureType;
        StructureTypeOther = structureTypeOther;
        RoofFrameType = roofFrameType;
        RoofFrameTypeOther = roofFrameTypeOther;
        RoofType = roofType;
        RoofTypeOther = roofTypeOther;
        CeilingType = ceilingType;
        CeilingTypeOther = ceilingTypeOther;
        InteriorWallType = interiorWallType;
        InteriorWallTypeOther = interiorWallTypeOther;
        ExteriorWallType = exteriorWallType;
        ExteriorWallTypeOther = exteriorWallTypeOther;
        FenceType = fenceType;
        FenceTypeOther = fenceTypeOther;
        ConstructionType = constructionType;
        ConstructionTypeOther = constructionTypeOther;

        // Utilization
        UtilizationType = utilizationType;
        UtilizationTypeOther = utilizationTypeOther;

        // Area & Pricing
        TotalBuildingArea = totalBuildingArea;
        BuildingCostValue = buildingCostValue;
        BuildingInsurancePrice = buildingInsurancePrice;

        // Other
        Remark = remark;
    }

    public static BuildingAppraisalDetail CopyFrom(BuildingAppraisalDetail source, Guid newPropertyId)
    {
        var copy = new BuildingAppraisalDetail
        {
            AppraisalPropertyId = newPropertyId,
            PropertyName = source.PropertyName,
            BuildingNumber = source.BuildingNumber,
            ModelName = source.ModelName,
            BuiltOnTitleNumber = source.BuiltOnTitleNumber,
            HouseNumber = source.HouseNumber,
            NoHouseNumber = source.NoHouseNumber,
            OwnerName = source.OwnerName,
            IsOwnerVerified = source.IsOwnerVerified,
            HasObligation = source.HasObligation,
            ObligationDetails = source.ObligationDetails,
            BuildingConditionType = source.BuildingConditionType,
            BuildingConditionTypeOther = source.BuildingConditionTypeOther,
            IsUnderConstruction = source.IsUnderConstruction,
            ConstructionLicenseExpirationDate = source.ConstructionLicenseExpirationDate,
            IsAppraisable = source.IsAppraisable,
            BuildingType = source.BuildingType,
            BuildingTypeOther = source.BuildingTypeOther,
            NumberOfFloors = source.NumberOfFloors,
            DecorationType = source.DecorationType,
            DecorationTypeOther = source.DecorationTypeOther,
            IsEncroachingOthers = source.IsEncroachingOthers,
            EncroachingOthersRemark = source.EncroachingOthersRemark,
            EncroachingOthersArea = source.EncroachingOthersArea,
            BuildingMaterialType = source.BuildingMaterialType,
            BuildingStyleType = source.BuildingStyleType,
            BuildingStyleTypeOther = source.BuildingStyleTypeOther,
            IsResidential = source.IsResidential,
            BuildingAge = source.BuildingAge,
            ConstructionYear = source.ConstructionYear,
            ResidentialRemark = source.ResidentialRemark,
            ConstructionStyleType = source.ConstructionStyleType,
            ConstructionStyleRemark = source.ConstructionStyleRemark,
            StructureType = source.StructureType?.ToList(),
            StructureTypeOther = source.StructureTypeOther,
            RoofFrameType = source.RoofFrameType?.ToList(),
            RoofFrameTypeOther = source.RoofFrameTypeOther,
            RoofType = source.RoofType?.ToList(),
            RoofTypeOther = source.RoofTypeOther,
            CeilingType = source.CeilingType?.ToList(),
            CeilingTypeOther = source.CeilingTypeOther,
            InteriorWallType = source.InteriorWallType?.ToList(),
            InteriorWallTypeOther = source.InteriorWallTypeOther,
            ExteriorWallType = source.ExteriorWallType?.ToList(),
            ExteriorWallTypeOther = source.ExteriorWallTypeOther,
            FenceType = source.FenceType?.ToList(),
            FenceTypeOther = source.FenceTypeOther,
            ConstructionType = source.ConstructionType,
            ConstructionTypeOther = source.ConstructionTypeOther,
            UtilizationType = source.UtilizationType,
            UtilizationTypeOther = source.UtilizationTypeOther,
            TotalBuildingArea = source.TotalBuildingArea,
            BuildingCostValue = source.BuildingCostValue,
            BuildingInsurancePrice = source.BuildingInsurancePrice,
            Remark = source.Remark
        };

        foreach (var dep in source.DepreciationDetails)
        {
            var depCopy = BuildingDepreciationDetail.Create(
                copy.Id, dep.DepreciationMethod, dep.AreaDescription, dep.Area, dep.Year,
                dep.IsBuilding, dep.PricePerSqMBeforeDepreciation, dep.PriceBeforeDepreciation,
                dep.PricePerSqMAfterDepreciation, dep.PriceAfterDepreciation,
                dep.DepreciationYearPct, dep.TotalDepreciationPct, dep.PriceDepreciation);

            foreach (var period in dep.DepreciationPeriods)
            {
                depCopy.AddPeriod(
                    period.AtYear, period.ToYear, period.DepreciationPerYear,
                    period.TotalDepreciationPct, period.PriceDepreciation);
            }

            copy._depreciationDetails.Add(depCopy);
        }

        foreach (var surface in source.Surfaces)
        {
            var surfaceCopy = BuildingAppraisalSurface.Create(
                copy.Id, surface.FromFloorNumber, surface.ToFloorNumber,
                surface.FloorType, surface.FloorStructureType, surface.FloorStructureTypeOther,
                surface.FloorSurfaceType, surface.FloorSurfaceTypeOther);
            copy._surfaces.Add(surfaceCopy);
        }

        return copy;
    }

    public BuildingDepreciationDetail AddDepreciationDetail(
        string depreciationMethod,
        string? areaDescription = null,
        decimal area = 0,
        short year = 0,
        bool isBuilding = true,
        decimal pricePerSqMBeforeDepreciation = 0,
        decimal priceBeforeDepreciation = 0,
        decimal pricePerSqMAfterDepreciation = 0,
        decimal priceAfterDepreciation = 0,
        decimal depreciationYearPct = 0,
        decimal totalDepreciationPct = 0,
        decimal priceDepreciation = 0)
    {
        var detail = BuildingDepreciationDetail.Create(
            Id, depreciationMethod, areaDescription, area, year, isBuilding,
            pricePerSqMBeforeDepreciation, priceBeforeDepreciation, pricePerSqMAfterDepreciation,
            priceAfterDepreciation, depreciationYearPct, totalDepreciationPct, priceDepreciation);
        _depreciationDetails.Add(detail);
        return detail;
    }

    /// <summary>
    /// Seeds บ้านเลขที่ at appraisal creation. Not <see cref="Update"/>: that overwrites every field,
    /// including <see cref="IsAppraisable"/>'s default of true.
    /// </summary>
    internal void SetHouseNumber(string? houseNumber) =>
        HouseNumber = string.IsNullOrWhiteSpace(houseNumber) ? null : houseNumber.Trim();

    /// <summary>
    /// Fire-insurance value derived from the depreciation schedule: the IsBuilding rows' depreciated value,
    /// rounded to the nearest 1,000 (the same way SQL <c>ROUND(x, -3)</c> does, midpoint away from zero).
    /// Each row already holds the 2 decimals the column stores (<see cref="BuildingDepreciationDetail"/> rounds
    /// on the way in), so a value computed before save matches the one recomputed from the saved rows (and the
    /// screen's, which does the same). Null when there are no
    /// IsBuilding rows. This is the one definition; the valuation summary uses it too, and
    /// <c>BuildingInsuranceCalculator</c> mirrors it in SQL.
    /// </summary>
    public decimal? ComputeInsurancePrice() => RoundedDepreciatedValue(_depreciationDetails.Where(d => d.IsBuilding));

    /// <summary>
    /// Building Cost Value derived from the depreciation schedule: the depreciated value of EVERY row
    /// (Non-Building included, which is what the Cost approach prices), rounded like
    /// <see cref="ComputeInsurancePrice"/>. Null when there are no rows. Mirrored in SQL as
    /// <c>ROUND(SUM(bdd.PriceAfterDepreciation), -3)</c> by every reader that still falls back for a NULL.
    /// </summary>
    public decimal? ComputeBuildingCostValue() => RoundedDepreciatedValue(_depreciationDetails);

    private static decimal? RoundedDepreciatedValue(IEnumerable<BuildingDepreciationDetail> source)
    {
        var rows = source.ToList();
        if (rows.Count == 0) return null;
        var total = rows.Sum(d => d.PriceAfterDepreciation);
        return Math.Round(total / 1000, MidpointRounding.AwayFromZero) * 1000;
    }

    /// <summary>
    /// Makes <see cref="BuildingInsurancePrice"/> and <see cref="BuildingCostValue"/> the values shown on
    /// screen: the appraiser's typed value when there is one, otherwise the computed one. Call after
    /// <see cref="Update"/> and after the depreciation rows are synced; a null typed value then
    /// re-computes from the current rows on every save.
    /// </summary>
    public void ResolveDerivedValues()
    {
        BuildingInsurancePrice ??= ComputeInsurancePrice();
        BuildingCostValue ??= ComputeBuildingCostValue();
    }

    public void RemoveDepreciationDetail(Guid depreciationDetailId)
    {
        var detail = _depreciationDetails.FirstOrDefault(d => d.Id == depreciationDetailId)
                     ?? throw new InvalidOperationException(
                         $"Depreciation detail {depreciationDetailId} not found");
        _depreciationDetails.Remove(detail);
    }

    public BuildingAppraisalSurface AddSurface(
        int fromFloorNumber,
        int toFloorNumber,
        string? floorType = null,
        string? floorStructureType = null,
        string? floorStructureTypeOther = null,
        string? floorSurfaceType = null,
        string? floorSurfaceTypeOther = null)
    {
        var surface = BuildingAppraisalSurface.Create(
            Id, fromFloorNumber, toFloorNumber, floorType,
            floorStructureType, floorStructureTypeOther, floorSurfaceType, floorSurfaceTypeOther);
        _surfaces.Add(surface);
        return surface;
    }

    public void RemoveSurface(Guid surfaceId)
    {
        var surface = _surfaces.FirstOrDefault(s => s.Id == surfaceId)
                      ?? throw new InvalidOperationException($"Surface {surfaceId} not found");
        _surfaces.Remove(surface);
    }
}
