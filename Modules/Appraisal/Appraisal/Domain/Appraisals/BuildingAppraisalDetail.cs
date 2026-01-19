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

    // Owner
    public string? OwnerName { get; private set; }
    public bool? IsOwnerVerified { get; private set; }
    public bool? HasObligation { get; private set; }
    public string? ObligationDetails { get; private set; }

    // Building Status
    public string? BuildingConditionType { get; private set; }
    public bool? IsUnderConstruction { get; private set; }
    public decimal? ConstructionCompletionPercent { get; private set; }
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
    public decimal? BuildingInsurancePrice { get; private set; }
    public decimal? SellingPrice { get; private set; }
    public decimal? ForcedSalePrice { get; private set; }

    // Other
    public string? Remark { get; private set; }

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
        // Owner
        string? ownerName = null,
        bool? isOwnerVerified = null,
        bool? hasObligation = null,
        string? obligationDetails = null,
        // Building Status
        string? buildingCondition = null,
        bool? isUnderConstruction = null,
        decimal? constructionCompletionPercent = null,
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
        string? buildingMaterial = null,
        string? buildingStyle = null,
        bool? isResidential = null,
        int? buildingAge = null,
        int? constructionYear = null,
        string? isResidentialRemark = null,
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
        string? otherPurposeUsage = null,
        // Area & Pricing
        decimal? totalBuildingArea = null,
        decimal? buildingInsurancePrice = null,
        decimal? sellingPrice = null,
        decimal? forcedSalePrice = null,
        // Other
        string? remark = null)
    {
        // Property Identification
        PropertyName = propertyName;
        BuildingNumber = buildingNumber;
        ModelName = modelName;
        BuiltOnTitleNumber = builtOnTitleNumber;
        HouseNumber = houseNumber;

        // Owner
        OwnerName = ownerName;
        IsOwnerVerified = isOwnerVerified;
        HasObligation = hasObligation;
        ObligationDetails = obligationDetails;

        // Building Status
        BuildingConditionType = buildingCondition;
        IsUnderConstruction = isUnderConstruction;
        ConstructionCompletionPercent = constructionCompletionPercent;

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
        BuildingMaterialType = buildingMaterial;
        BuildingStyleType = buildingStyle;
        IsResidential = isResidential;
        BuildingAge = buildingAge;
        ConstructionYear = constructionYear;
        ResidentialRemark = isResidentialRemark;
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
        UtilizationTypeOther = otherPurposeUsage;

        // Area & Pricing
        TotalBuildingArea = totalBuildingArea;
        BuildingInsurancePrice = buildingInsurancePrice;
        SellingPrice = sellingPrice;
        ForcedSalePrice = forcedSalePrice;

        // Other
        Remark = remark;
    }
}