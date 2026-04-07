namespace Appraisal.Domain.Appraisals;

public class CondoTower : Entity<Guid>
{
    public Guid AppraisalId { get; private set; }

    // Tower Identification
    public string? TowerName { get; private set; }
    public int? NumberOfUnits { get; private set; }
    public int? NumberOfFloors { get; private set; }
    public string? CondoRegistrationNumber { get; private set; }
    public List<Guid>? ModelTypeIds { get; private set; }

    // Condition & Obligation
    public string? ConditionType { get; private set; }
    public bool? HasObligation { get; private set; }
    public string? ObligationDetails { get; private set; }
    public string? DocumentValidationType { get; private set; }

    // Location
    public bool? IsLocationCorrect { get; private set; }
    public decimal? Distance { get; private set; }
    public decimal? RoadWidth { get; private set; }
    public decimal? RightOfWay { get; private set; }
    public string? RoadSurfaceType { get; private set; }
    public string? RoadSurfaceTypeOther { get; private set; }

    // Decoration
    public string? DecorationType { get; private set; }
    public string? DecorationTypeOther { get; private set; }

    // Building Info
    public int? ConstructionYear { get; private set; }
    public int? TotalNumberOfFloors { get; private set; }
    public string? BuildingFormType { get; private set; }
    public string? ConstructionMaterialType { get; private set; }

    // Materials
    public string? GroundFloorMaterialType { get; private set; }
    public string? GroundFloorMaterialTypeOther { get; private set; }
    public string? UpperFloorMaterialType { get; private set; }
    public string? UpperFloorMaterialTypeOther { get; private set; }
    public string? BathroomFloorMaterialType { get; private set; }
    public string? BathroomFloorMaterialTypeOther { get; private set; }
    public List<string>? RoofType { get; private set; }
    public string? RoofTypeOther { get; private set; }

    // Legal Restrictions
    public bool? IsExpropriated { get; private set; }
    public string? ExpropriationRemark { get; private set; }
    public bool? IsInExpropriationLine { get; private set; }
    public string? RoyalDecree { get; private set; }
    public bool? IsForestBoundary { get; private set; }
    public string? ForestBoundaryRemark { get; private set; }

    // Other
    public string? Remark { get; private set; }
    public List<Guid>? ImageDocumentIds { get; private set; }

    private CondoTower()
    {
    }

    public static CondoTower Create(Guid appraisalId)
    {
        return new CondoTower
        {
            Id = Guid.CreateVersion7(),
            AppraisalId = appraisalId
        };
    }

    public static CondoTower Create(Guid appraisalId, string towerName)
    {
        return new CondoTower
        {
            Id = Guid.CreateVersion7(),
            AppraisalId = appraisalId,
            TowerName = towerName
        };
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public void Update(
        // Tower Identification
        string? towerName = null,
        int? numberOfUnits = null,
        int? numberOfFloors = null,
        string? condoRegistrationNumber = null,
        List<Guid>? modelTypeIds = null,
        // Condition & Obligation
        string? conditionType = null,
        bool? hasObligation = null,
        string? obligationDetails = null,
        string? documentValidationType = null,
        // Location
        bool? isLocationCorrect = null,
        decimal? distance = null,
        decimal? roadWidth = null,
        decimal? rightOfWay = null,
        string? roadSurfaceType = null,
        string? roadSurfaceTypeOther = null,
        // Decoration
        string? decorationType = null,
        string? decorationTypeOther = null,
        // Building Info
        int? constructionYear = null,
        int? totalNumberOfFloors = null,
        string? buildingFormType = null,
        string? constructionMaterialType = null,
        // Materials
        string? groundFloorMaterialType = null,
        string? groundFloorMaterialTypeOther = null,
        string? upperFloorMaterialType = null,
        string? upperFloorMaterialTypeOther = null,
        string? bathroomFloorMaterialType = null,
        string? bathroomFloorMaterialTypeOther = null,
        List<string>? roofType = null,
        string? roofTypeOther = null,
        // Legal Restrictions
        bool? isExpropriated = null,
        string? expropriationRemark = null,
        bool? isInExpropriationLine = null,
        string? royalDecree = null,
        bool? isForestBoundary = null,
        string? forestBoundaryRemark = null,
        // Other
        string? remark = null,
        List<Guid>? imageDocumentIds = null)
    {
        // Validation
        if (numberOfUnits is < 0)
            throw new ArgumentException("Number of units cannot be negative", nameof(numberOfUnits));
        if (numberOfFloors is < 0)
            throw new ArgumentException("Number of floors cannot be negative", nameof(numberOfFloors));
        if (totalNumberOfFloors is < 0)
            throw new ArgumentException("Total number of floors cannot be negative", nameof(totalNumberOfFloors));

        // Tower Identification
        TowerName = towerName;
        NumberOfUnits = numberOfUnits;
        NumberOfFloors = numberOfFloors;
        CondoRegistrationNumber = condoRegistrationNumber;
        ModelTypeIds = modelTypeIds;

        // Condition & Obligation
        ConditionType = conditionType;
        HasObligation = hasObligation;
        ObligationDetails = obligationDetails;
        DocumentValidationType = documentValidationType;

        // Location
        IsLocationCorrect = isLocationCorrect;
        Distance = distance;
        RoadWidth = roadWidth;
        RightOfWay = rightOfWay;
        RoadSurfaceType = roadSurfaceType;
        RoadSurfaceTypeOther = roadSurfaceTypeOther;

        // Decoration
        DecorationType = decorationType;
        DecorationTypeOther = decorationTypeOther;

        // Building Info
        ConstructionYear = constructionYear;
        TotalNumberOfFloors = totalNumberOfFloors;
        BuildingFormType = buildingFormType;
        ConstructionMaterialType = constructionMaterialType;

        // Materials
        GroundFloorMaterialType = groundFloorMaterialType;
        GroundFloorMaterialTypeOther = groundFloorMaterialTypeOther;
        UpperFloorMaterialType = upperFloorMaterialType;
        UpperFloorMaterialTypeOther = upperFloorMaterialTypeOther;
        BathroomFloorMaterialType = bathroomFloorMaterialType;
        BathroomFloorMaterialTypeOther = bathroomFloorMaterialTypeOther;
        RoofType = roofType;
        RoofTypeOther = roofTypeOther;

        // Legal Restrictions
        IsExpropriated = isExpropriated;
        ExpropriationRemark = expropriationRemark;
        IsInExpropriationLine = isInExpropriationLine;
        RoyalDecree = royalDecree;
        IsForestBoundary = isForestBoundary;
        ForestBoundaryRemark = forestBoundaryRemark;

        // Other
        Remark = remark;
        ImageDocumentIds = imageDocumentIds;
    }
}
