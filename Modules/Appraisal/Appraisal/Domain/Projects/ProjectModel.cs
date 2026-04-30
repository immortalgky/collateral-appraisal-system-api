using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Projects.Exceptions;

namespace Appraisal.Domain.Projects;

/// <summary>
/// Superset of CondoModel + VillageModel.
/// Common fields are always populated. LB-specific fields (nullable) are only
/// meaningful when the owning Project has ProjectType = LandAndBuilding.
/// Surfaces and DepreciationDetails are also LB-only collections.
/// </summary>
public class ProjectModel : Entity<Guid>
{
    public Guid ProjectId { get; private set; }

    // ----- Common Fields -----
    public string? ModelName { get; private set; }
    public string? ModelDescription { get; private set; }

    // Condo uses BuildingNumber; LB uses NumberOfHouse — both kept as nullable
    public string? BuildingNumber { get; private set; }   // Condo
    public int? NumberOfHouse { get; private set; }       // LB

    // Pricing — Condo has Min/Max; LB has a single StartingPrice
    public decimal? StartingPrice { get; private set; }   // LB
    public decimal? StartingPriceMin { get; private set; } // Condo
    public decimal? StartingPriceMax { get; private set; } // Condo

    // Standard price is derived from PricingAnalysis.FinalAppraisedValue (no longer stored here).
    // Navigate via PricingAnalysis?.FinalAppraisedValue.
    public PricingAnalysis? PricingAnalysis { get; private set; }

    public bool? HasMezzanine { get; private set; }

    // Usable Area
    public decimal? UsableAreaMin { get; private set; }
    public decimal? UsableAreaMax { get; private set; }
    public decimal? StandardUsableArea { get; private set; }

    // Insurance
    public string? FireInsuranceCondition { get; private set; }

    // Layout (Condo)
    public string? RoomLayoutType { get; private set; }
    public string? RoomLayoutTypeOther { get; private set; }

    // Floor Materials (both types)
    public string? GroundFloorMaterialType { get; private set; }
    public string? GroundFloorMaterialTypeOther { get; private set; }
    public string? UpperFloorMaterialType { get; private set; }
    public string? UpperFloorMaterialTypeOther { get; private set; }
    public string? BathroomFloorMaterialType { get; private set; }
    public string? BathroomFloorMaterialTypeOther { get; private set; }

    // Other
    public string? Remark { get; private set; }

    // ----- LandAndBuilding-Specific Fields (nullable) -----

    // Land Area (LB)
    public decimal? LandAreaRai { get; private set; }
    public decimal? LandAreaNgan { get; private set; }
    public decimal? LandAreaWa { get; private set; }
    public decimal? StandardLandArea { get; private set; } // in Sq.wa

    // Building Info (LB)
    public string? BuildingType { get; private set; }
    public string? BuildingTypeOther { get; private set; }
    public decimal? NumberOfFloors { get; private set; }
    public string? DecorationType { get; private set; }
    public string? DecorationTypeOther { get; private set; }
    public bool? IsEncroachingOthers { get; private set; }
    public string? EncroachingOthersRemark { get; private set; }
    public decimal? EncroachingOthersArea { get; private set; }

    // Construction Details (LB)
    public string? BuildingMaterialType { get; private set; }
    public string? BuildingStyleType { get; private set; }
    public bool? IsResidential { get; private set; }
    public int? BuildingAge { get; private set; }
    public int? ConstructionYear { get; private set; }
    public string? ResidentialRemark { get; private set; }
    public string? ConstructionStyleType { get; private set; }
    public string? ConstructionStyleRemark { get; private set; }

    // Structure Components (LB)
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

    // Utilization (LB)
    public string? UtilizationType { get; private set; }
    public string? UtilizationTypeOther { get; private set; }

    // ----- Owned Collections -----

    private readonly List<ProjectModelImage> _images = [];
    public IReadOnlyList<ProjectModelImage> Images => _images.AsReadOnly();

    private readonly List<ProjectModelAreaDetail> _areaDetails = [];
    public IReadOnlyList<ProjectModelAreaDetail> AreaDetails => _areaDetails.AsReadOnly();

    // LB-only collections (empty for Condo projects)
    private readonly List<ProjectModelSurface> _surfaces = [];
    public IReadOnlyList<ProjectModelSurface> Surfaces => _surfaces.AsReadOnly();

    private readonly List<ProjectModelDepreciationDetail> _depreciationDetails = [];
    public IReadOnlyList<ProjectModelDepreciationDetail> DepreciationDetails => _depreciationDetails.AsReadOnly();

    private ProjectModel()
    {
    }

    public static ProjectModel Create(Guid projectId)
    {
        return new ProjectModel
        {
            Id = Guid.CreateVersion7(),
            ProjectId = projectId
        };
    }

    public static ProjectModel Create(Guid projectId, string modelName)
    {
        return new ProjectModel
        {
            Id = Guid.CreateVersion7(),
            ProjectId = projectId,
            ModelName = modelName
        };
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public void Update(
        // Common
        string? modelName = null,
        string? modelDescription = null,
        string? buildingNumber = null,
        int? numberOfHouse = null,
        decimal? startingPrice = null,
        decimal? startingPriceMin = null,
        decimal? startingPriceMax = null,
        bool? hasMezzanine = null,
        decimal? usableAreaMin = null,
        decimal? usableAreaMax = null,
        decimal? standardUsableArea = null,
        string? fireInsuranceCondition = null,
        string? roomLayoutType = null,
        string? roomLayoutTypeOther = null,
        string? groundFloorMaterialType = null,
        string? groundFloorMaterialTypeOther = null,
        string? upperFloorMaterialType = null,
        string? upperFloorMaterialTypeOther = null,
        string? bathroomFloorMaterialType = null,
        string? bathroomFloorMaterialTypeOther = null,
        string? remark = null,
        // LB-specific (nullable — ignored when Condo)
        decimal? landAreaRai = null,
        decimal? landAreaNgan = null,
        decimal? landAreaWa = null,
        decimal? standardLandArea = null,
        string? buildingType = null,
        string? buildingTypeOther = null,
        decimal? numberOfFloors = null,
        string? decorationType = null,
        string? decorationTypeOther = null,
        bool? isEncroachingOthers = null,
        string? encroachingOthersRemark = null,
        decimal? encroachingOthersArea = null,
        string? buildingMaterialType = null,
        string? buildingStyleType = null,
        bool? isResidential = null,
        int? buildingAge = null,
        int? constructionYear = null,
        string? residentialRemark = null,
        string? constructionStyleType = null,
        string? constructionStyleRemark = null,
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
        string? utilizationType = null,
        string? utilizationTypeOther = null)
    {
        // Validation
        if (startingPriceMin.HasValue && startingPriceMax.HasValue && startingPriceMin > startingPriceMax)
            throw new ArgumentException("Starting price min cannot exceed starting price max", nameof(startingPriceMin));
        if (usableAreaMin.HasValue && usableAreaMax.HasValue && usableAreaMin > usableAreaMax)
            throw new ArgumentException("Usable area min cannot exceed usable area max", nameof(usableAreaMin));
        if (standardUsableArea is < 0)
            throw new ArgumentException("Standard usable area cannot be negative", nameof(standardUsableArea));
        // Common
        ModelName = modelName;
        ModelDescription = modelDescription;
        BuildingNumber = buildingNumber;
        NumberOfHouse = numberOfHouse;
        StartingPrice = startingPrice;
        StartingPriceMin = startingPriceMin;
        StartingPriceMax = startingPriceMax;
        HasMezzanine = hasMezzanine;
        UsableAreaMin = usableAreaMin;
        UsableAreaMax = usableAreaMax;
        StandardUsableArea = standardUsableArea;
        FireInsuranceCondition = fireInsuranceCondition;
        RoomLayoutType = roomLayoutType;
        RoomLayoutTypeOther = roomLayoutTypeOther;
        GroundFloorMaterialType = groundFloorMaterialType;
        GroundFloorMaterialTypeOther = groundFloorMaterialTypeOther;
        UpperFloorMaterialType = upperFloorMaterialType;
        UpperFloorMaterialTypeOther = upperFloorMaterialTypeOther;
        BathroomFloorMaterialType = bathroomFloorMaterialType;
        BathroomFloorMaterialTypeOther = bathroomFloorMaterialTypeOther;
        Remark = remark;

        // LB-specific
        LandAreaRai = landAreaRai;
        LandAreaNgan = landAreaNgan;
        LandAreaWa = landAreaWa;
        StandardLandArea = standardLandArea;
        BuildingType = buildingType;
        BuildingTypeOther = buildingTypeOther;
        NumberOfFloors = numberOfFloors;
        DecorationType = decorationType;
        DecorationTypeOther = decorationTypeOther;
        IsEncroachingOthers = isEncroachingOthers;
        EncroachingOthersRemark = encroachingOthersRemark;
        EncroachingOthersArea = encroachingOthersArea;
        BuildingMaterialType = buildingMaterialType;
        BuildingStyleType = buildingStyleType;
        IsResidential = isResidential;
        BuildingAge = buildingAge;
        ConstructionYear = constructionYear;
        ResidentialRemark = residentialRemark;
        ConstructionStyleType = constructionStyleType;
        ConstructionStyleRemark = constructionStyleRemark;
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
        UtilizationType = utilizationType;
        UtilizationTypeOther = utilizationTypeOther;
    }

    // --- Area Details ---

    public void AddAreaDetail(ProjectModelAreaDetail areaDetail)
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

    // --- Surfaces (LB only) ---

    public ProjectModelSurface AddSurface(
        int fromFloorNumber,
        int toFloorNumber,
        string? floorType = null,
        string? floorStructureType = null,
        string? floorStructureTypeOther = null,
        string? floorSurfaceType = null,
        string? floorSurfaceTypeOther = null)
    {
        var surface = ProjectModelSurface.Create(
            Id, fromFloorNumber, toFloorNumber, floorType,
            floorStructureType, floorStructureTypeOther, floorSurfaceType, floorSurfaceTypeOther);
        _surfaces.Add(surface);
        return surface;
    }

    public void RemoveSurface(Guid surfaceId)
    {
        var surface = _surfaces.FirstOrDefault(s => s.Id == surfaceId)
                      ?? throw new InvalidProjectStateException($"Surface {surfaceId} not found");
        _surfaces.Remove(surface);
    }

    // --- Depreciation Details (LB only) ---

    public ProjectModelDepreciationDetail AddDepreciationDetail(
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
        var detail = ProjectModelDepreciationDetail.Create(
            Id, depreciationMethod, areaDescription, area, year, isBuilding,
            pricePerSqMBeforeDepreciation, priceBeforeDepreciation, pricePerSqMAfterDepreciation,
            priceAfterDepreciation, depreciationYearPct, totalDepreciationPct, priceDepreciation);
        _depreciationDetails.Add(detail);
        return detail;
    }

    public void RemoveDepreciationDetail(Guid depreciationDetailId)
    {
        var detail = _depreciationDetails.FirstOrDefault(d => d.Id == depreciationDetailId)
                     ?? throw new InvalidProjectStateException(
                         $"Depreciation detail {depreciationDetailId} not found");
        _depreciationDetails.Remove(detail);
    }

    // --- Images ---

    public ProjectModelImage AddImage(
        Guid galleryPhotoId,
        string? title = null,
        string? description = null)
    {
        var nextSequence = _images.Count > 0 ? _images.Max(i => i.DisplaySequence) + 1 : 1;
        var image = ProjectModelImage.Create(Id, nextSequence, galleryPhotoId, title, description);
        if (!_images.Any(i => i.IsThumbnail)) image.SetAsThumbnail();
        _images.Add(image);
        return image;
    }

    public void RemoveImage(Guid imageId)
    {
        var image = _images.FirstOrDefault(i => i.Id == imageId)
                    ?? throw new InvalidProjectStateException($"Image {imageId} not found on model {Id}");
        _images.Remove(image);
    }

    public void ReorderImages(IEnumerable<(Guid ImageId, int NewSequence)> reorder)
    {
        foreach (var (imageId, newSequence) in reorder)
        {
            var image = _images.FirstOrDefault(i => i.Id == imageId);
            image?.UpdateSequence(newSequence);
        }
    }

    public void SetThumbnail(Guid imageId)
    {
        var target = _images.FirstOrDefault(i => i.Id == imageId)
                     ?? throw new InvalidProjectStateException($"Image {imageId} not found on model {Id}");

        // Enforce single-thumbnail invariant: unset any existing thumbnail first
        foreach (var img in _images.Where(i => i.IsThumbnail))
            img.UnsetAsThumbnail();

        target.SetAsThumbnail();
    }

    public void UnsetThumbnail(Guid imageId)
    {
        var target = _images.FirstOrDefault(i => i.Id == imageId)
                     ?? throw new InvalidProjectStateException($"Image {imageId} not found on model {Id}");
        target.UnsetAsThumbnail();
    }
}
