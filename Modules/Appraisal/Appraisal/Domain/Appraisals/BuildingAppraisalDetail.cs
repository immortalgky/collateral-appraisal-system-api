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
    public string OwnerName { get; private set; } = null!;
    public bool IsOwnerVerified { get; private set; }
    public bool HasObligation { get; private set; }
    public string? ObligationDetails { get; private set; }

    // Building Status
    public string? BuildingConditionType { get; private set; }
    public bool IsUnderConstruction { get; private set; }
    public decimal? ConstructionCompletionPercent { get; private set; }
    public DateTime? ConstructionLicenseExpirationDate { get; private set; }
    public bool IsAppraisable { get; private set; } = true;

    // Building Info
    public string? BuildingType { get; private set; }
    public string? BuildingTypeOther { get; private set; }
    public int? NumberOfFloors { get; private set; }
    public string? DecorationType { get; private set; }
    public string? DecorationTypeOther { get; private set; }
    public bool IsEncroached { get; private set; }
    public string? EncroachmentRemark { get; private set; }
    public decimal? EncroachmentArea { get; private set; }

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

    // Area & Pricing
    public decimal? TotalBuildingArea { get; private set; }
    public decimal? BuildingInsurancePrice { get; private set; }
    public decimal? SellingPrice { get; private set; }
    public decimal? ForcedSalePrice { get; private set; }

    // Other
    public string? Remark { get; private set; }

    private BuildingAppraisalDetail()
    {
    }

    public static BuildingAppraisalDetail Create(
        Guid appraisalPropertyId,
        string ownerName,
        Guid createdBy)
    {
        return new BuildingAppraisalDetail
        {
            Id = Guid.NewGuid(),
            AppraisalPropertyId = appraisalPropertyId,
            OwnerName = ownerName,
            IsAppraisable = true
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
        int? numberOfFloors = null,
        string? decorationType = null,
        string? decorationTypeOther = null,
        bool? isEncroached = null,
        string? encroachmentRemark = null,
        decimal? encroachmentArea = null,
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
        if (isOwnerVerified.HasValue) IsOwnerVerified = isOwnerVerified.Value;
        if (hasObligation.HasValue) HasObligation = hasObligation.Value;
        ObligationDetails = obligationDetails;

        // Building Status
        BuildingConditionType = buildingCondition;
        if (isUnderConstruction.HasValue) IsUnderConstruction = isUnderConstruction.Value;
        if (constructionCompletionPercent.HasValue) ConstructionCompletionPercent = constructionCompletionPercent.Value;
        if (constructionLicenseExpirationDate.HasValue)
            ConstructionLicenseExpirationDate = constructionLicenseExpirationDate.Value;
        if (isAppraisable.HasValue) IsAppraisable = isAppraisable.Value;

        // Building Info
        BuildingType = buildingType;
        BuildingTypeOther = buildingTypeOther;
        if (numberOfFloors.HasValue) NumberOfFloors = numberOfFloors.Value;
        DecorationType = decorationType;
        DecorationTypeOther = decorationTypeOther;
        if (isEncroached.HasValue) IsEncroached = isEncroached.Value;
        EncroachmentRemark = encroachmentRemark;
        if (encroachmentArea.HasValue) EncroachmentArea = encroachmentArea.Value;

        // Construction Details
        BuildingMaterialType = buildingMaterial;
        BuildingStyleType = buildingStyle;
        if (isResidential.HasValue) IsResidential = isResidential.Value;
        if (buildingAge.HasValue) BuildingAge = buildingAge.Value;
        if (constructionYear.HasValue) ConstructionYear = constructionYear.Value;
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
        if (totalBuildingArea.HasValue) TotalBuildingArea = totalBuildingArea.Value;
        if (buildingInsurancePrice.HasValue) BuildingInsurancePrice = buildingInsurancePrice.Value;
        if (sellingPrice.HasValue) SellingPrice = sellingPrice.Value;
        if (forcedSalePrice.HasValue) ForcedSalePrice = forcedSalePrice.Value;

        // Other
        Remark = remark;
    }
}