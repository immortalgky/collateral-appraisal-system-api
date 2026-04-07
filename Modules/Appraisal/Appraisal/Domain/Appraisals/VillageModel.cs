namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Village project house model combining CondoModel-style model info
/// with BuildingAppraisalDetail building fields.
/// Owned by Appraisal (1:M — one appraisal has many models).
/// </summary>
public class VillageModel : Entity<Guid>
{
    public Guid AppraisalId { get; private set; }

    // Model Info
    public string? ModelName { get; private set; }
    public string? ModelDescription { get; private set; }
    public int? NumberOfHouse { get; private set; }
    public decimal? StartingPrice { get; private set; }

    // Usable Area
    public decimal? UsableAreaMin { get; private set; }
    public decimal? UsableAreaMax { get; private set; }
    public decimal? StandardUsableArea { get; private set; }

    // Land Area (Thai units)
    public decimal? LandAreaRai { get; private set; }
    public decimal? LandAreaNgan { get; private set; }
    public decimal? LandAreaWa { get; private set; }
    public decimal? StandardLandArea { get; private set; } // in Sq.wa

    // Insurance
    public string? FireInsuranceCondition { get; private set; }

    // Documents
    public List<Guid>? ImageDocumentIds { get; private set; }

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

    // Other
    public string? Remark { get; private set; }

    // Owned Collections
    private readonly List<VillageModelAreaDetail> _areaDetails = [];
    public IReadOnlyList<VillageModelAreaDetail> AreaDetails => _areaDetails.AsReadOnly();

    private readonly List<VillageModelSurface> _surfaces = [];
    public IReadOnlyList<VillageModelSurface> Surfaces => _surfaces.AsReadOnly();

    private readonly List<VillageModelDepreciationDetail> _depreciationDetails = [];
    public IReadOnlyList<VillageModelDepreciationDetail> DepreciationDetails => _depreciationDetails.AsReadOnly();

    private VillageModel()
    {
    }

    public static VillageModel Create(Guid appraisalId)
    {
        return new VillageModel
        {
            Id = Guid.CreateVersion7(),
            AppraisalId = appraisalId
        };
    }

    public static VillageModel Create(Guid appraisalId, string modelName)
    {
        return new VillageModel
        {
            Id = Guid.CreateVersion7(),
            AppraisalId = appraisalId,
            ModelName = modelName
        };
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public void Update(
        // Model Info
        string? modelName = null,
        string? modelDescription = null,
        int? numberOfHouse = null,
        decimal? startingPrice = null,
        // Usable Area
        decimal? usableAreaMin = null,
        decimal? usableAreaMax = null,
        decimal? standardUsableArea = null,
        // Land Area
        decimal? landAreaRai = null,
        decimal? landAreaNgan = null,
        decimal? landAreaWa = null,
        decimal? standardLandArea = null,
        // Insurance
        string? fireInsuranceCondition = null,
        // Documents
        List<Guid>? imageDocumentIds = null,
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
        // Other
        string? remark = null)
    {
        // Validation
        if (usableAreaMin.HasValue && usableAreaMax.HasValue && usableAreaMin > usableAreaMax)
            throw new ArgumentException("Usable area min cannot exceed usable area max", nameof(usableAreaMin));
        if (standardUsableArea is < 0)
            throw new ArgumentException("Standard usable area cannot be negative", nameof(standardUsableArea));

        // Model Info
        ModelName = modelName;
        ModelDescription = modelDescription;
        NumberOfHouse = numberOfHouse;
        StartingPrice = startingPrice;

        // Usable Area
        UsableAreaMin = usableAreaMin;
        UsableAreaMax = usableAreaMax;
        StandardUsableArea = standardUsableArea;

        // Land Area
        LandAreaRai = landAreaRai;
        LandAreaNgan = landAreaNgan;
        LandAreaWa = landAreaWa;
        StandardLandArea = standardLandArea;

        // Insurance
        FireInsuranceCondition = fireInsuranceCondition;

        // Documents
        ImageDocumentIds = imageDocumentIds;

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

        // Other
        Remark = remark;
    }

    // --- Area Details ---

    public void AddAreaDetail(VillageModelAreaDetail areaDetail)
    {
        _areaDetails.Add(areaDetail);
    }

    public void RemoveAreaDetail(Guid areaDetailId)
    {
        var item = _areaDetails.FirstOrDefault(a => a.Id == areaDetailId);
        if (item != null) _areaDetails.Remove(item);
    }

    public void ClearAreaDetails()
    {
        _areaDetails.Clear();
    }

    // --- Surfaces ---

    public VillageModelSurface AddSurface(
        int fromFloorNumber,
        int toFloorNumber,
        string? floorType = null,
        string? floorStructureType = null,
        string? floorStructureTypeOther = null,
        string? floorSurfaceType = null,
        string? floorSurfaceTypeOther = null)
    {
        var surface = VillageModelSurface.Create(
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

    // --- Depreciation Details ---

    public VillageModelDepreciationDetail AddDepreciationDetail(
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
        var detail = VillageModelDepreciationDetail.Create(
            Id, depreciationMethod, areaDescription, area, year, isBuilding,
            pricePerSqMBeforeDepreciation, priceBeforeDepreciation, pricePerSqMAfterDepreciation,
            priceAfterDepreciation, depreciationYearPct, totalDepreciationPct, priceDepreciation);
        _depreciationDetails.Add(detail);
        return detail;
    }

    public void RemoveDepreciationDetail(Guid depreciationDetailId)
    {
        var detail = _depreciationDetails.FirstOrDefault(d => d.Id == depreciationDetailId)
                     ?? throw new InvalidOperationException(
                         $"Depreciation detail {depreciationDetailId} not found");
        _depreciationDetails.Remove(detail);
    }
}
