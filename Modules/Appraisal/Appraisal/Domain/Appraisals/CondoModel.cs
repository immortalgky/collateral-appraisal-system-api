namespace Appraisal.Domain.Appraisals;

public class CondoModel : Entity<Guid>
{
    public Guid AppraisalId { get; private set; }

    // Model Info
    public string? ModelName { get; private set; }
    public string? ModelDescription { get; private set; }
    public string? BuildingNumber { get; private set; }

    // Pricing
    public decimal? StartingPriceMin { get; private set; }
    public decimal? StartingPriceMax { get; private set; }
    public bool? HasMezzanine { get; private set; }

    // Usable Area
    public decimal? UsableAreaMin { get; private set; }
    public decimal? UsableAreaMax { get; private set; }
    public decimal? StandardUsableArea { get; private set; }

    // Insurance
    public string? FireInsuranceCondition { get; private set; }

    // Layout
    public string? RoomLayoutType { get; private set; }
    public string? RoomLayoutTypeOther { get; private set; }

    // Materials
    public string? GroundFloorMaterialType { get; private set; }
    public string? GroundFloorMaterialTypeOther { get; private set; }
    public string? UpperFloorMaterialType { get; private set; }
    public string? UpperFloorMaterialTypeOther { get; private set; }
    public string? BathroomFloorMaterialType { get; private set; }
    public string? BathroomFloorMaterialTypeOther { get; private set; }

    // Documents
    public List<Guid>? ImageDocumentIds { get; private set; }

    // Area Details (owned collection)
    private readonly List<CondoModelAreaDetail> _areaDetails = [];
    public IReadOnlyList<CondoModelAreaDetail> AreaDetails => _areaDetails.AsReadOnly();

    // Other
    public string? Remark { get; private set; }

    private CondoModel()
    {
    }

    public static CondoModel Create(Guid appraisalId)
    {
        return new CondoModel
        {
            Id = Guid.CreateVersion7(),
            AppraisalId = appraisalId
        };
    }

    public static CondoModel Create(Guid appraisalId, string modelName)
    {
        return new CondoModel
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
        string? buildingNumber = null,
        // Pricing
        decimal? startingPriceMin = null,
        decimal? startingPriceMax = null,
        bool? hasMezzanine = null,
        // Usable Area
        decimal? usableAreaMin = null,
        decimal? usableAreaMax = null,
        decimal? standardUsableArea = null,
        // Insurance
        string? fireInsuranceCondition = null,
        // Layout
        string? roomLayoutType = null,
        string? roomLayoutTypeOther = null,
        // Materials
        string? groundFloorMaterialType = null,
        string? groundFloorMaterialTypeOther = null,
        string? upperFloorMaterialType = null,
        string? upperFloorMaterialTypeOther = null,
        string? bathroomFloorMaterialType = null,
        string? bathroomFloorMaterialTypeOther = null,
        // Documents
        List<Guid>? imageDocumentIds = null,
        // Other
        string? remark = null)
    {
        // Validation
        if (startingPriceMin.HasValue && startingPriceMax.HasValue && startingPriceMin > startingPriceMax)
            throw new ArgumentException("Starting price min cannot exceed starting price max", nameof(startingPriceMin));
        if (usableAreaMin.HasValue && usableAreaMax.HasValue && usableAreaMin > usableAreaMax)
            throw new ArgumentException("Usable area min cannot exceed usable area max", nameof(usableAreaMin));
        if (standardUsableArea is < 0)
            throw new ArgumentException("Standard usable area cannot be negative", nameof(standardUsableArea));

        // Model Info
        ModelName = modelName;
        ModelDescription = modelDescription;
        BuildingNumber = buildingNumber;

        // Pricing
        StartingPriceMin = startingPriceMin;
        StartingPriceMax = startingPriceMax;
        HasMezzanine = hasMezzanine;

        // Usable Area
        UsableAreaMin = usableAreaMin;
        UsableAreaMax = usableAreaMax;
        StandardUsableArea = standardUsableArea;

        // Insurance
        FireInsuranceCondition = fireInsuranceCondition;

        // Layout
        RoomLayoutType = roomLayoutType;
        RoomLayoutTypeOther = roomLayoutTypeOther;

        // Materials
        GroundFloorMaterialType = groundFloorMaterialType;
        GroundFloorMaterialTypeOther = groundFloorMaterialTypeOther;
        UpperFloorMaterialType = upperFloorMaterialType;
        UpperFloorMaterialTypeOther = upperFloorMaterialTypeOther;
        BathroomFloorMaterialType = bathroomFloorMaterialType;
        BathroomFloorMaterialTypeOther = bathroomFloorMaterialTypeOther;

        // Documents
        ImageDocumentIds = imageDocumentIds;

        // Other
        Remark = remark;
    }

    public void AddAreaDetail(CondoModelAreaDetail areaDetail)
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
}
