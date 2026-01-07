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
    public string? BuildingCondition { get; private set; }
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
    public string? BuildingMaterial { get; private set; }
    public string? BuildingStyle { get; private set; }
    public bool? IsResidential { get; private set; }
    public int? BuildingAge { get; private set; }
    public int? ConstructionYear { get; private set; }
    public string? IsResidentialRemark { get; private set; }
    public string? ConstructionStyleType { get; private set; }
    public string? ConstructionStyleRemark { get; private set; }

    // Structure Components
    public string? StructureType { get; private set; }
    public string? StructureTypeOther { get; private set; }
    public string? RoofFrameType { get; private set; }
    public string? RoofFrameTypeOther { get; private set; }
    public string? RoofType { get; private set; }
    public string? RoofTypeOther { get; private set; }
    public string? CeilingType { get; private set; }
    public string? CeilingTypeOther { get; private set; }
    public string? InteriorWallType { get; private set; }
    public string? InteriorWallTypeOther { get; private set; }
    public string? ExteriorWallType { get; private set; }
    public string? ExteriorWallTypeOther { get; private set; }
    public string? FenceType { get; private set; }
    public string? FenceTypeOther { get; private set; }
    public string? ConstructionType { get; private set; }
    public string? ConstructionTypeOther { get; private set; }

    // Utilization
    public string? UtilizationType { get; private set; }
    public string? OtherPurposeUsage { get; private set; }

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
        string? structureType = null,
        string? structureTypeOther = null,
        string? roofFrameType = null,
        string? roofFrameTypeOther = null,
        string? roofType = null,
        string? roofTypeOther = null,
        string? ceilingType = null,
        string? ceilingTypeOther = null,
        string? interiorWallType = null,
        string? interiorWallTypeOther = null,
        string? exteriorWallType = null,
        string? exteriorWallTypeOther = null,
        string? fenceType = null,
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
        if (propertyName is not null) PropertyName = propertyName;
        if (buildingNumber is not null) BuildingNumber = buildingNumber;
        if (modelName is not null) ModelName = modelName;
        if (builtOnTitleNumber is not null) BuiltOnTitleNumber = builtOnTitleNumber;
        if (houseNumber is not null) HouseNumber = houseNumber;

        // Owner
        if (ownerName is not null) OwnerName = ownerName;
        if (isOwnerVerified.HasValue) IsOwnerVerified = isOwnerVerified.Value;
        if (hasObligation.HasValue) HasObligation = hasObligation.Value;
        if (obligationDetails is not null) ObligationDetails = obligationDetails;

        // Building Status
        if (buildingCondition is not null) BuildingCondition = buildingCondition;
        if (isUnderConstruction.HasValue) IsUnderConstruction = isUnderConstruction.Value;
        if (constructionCompletionPercent.HasValue) ConstructionCompletionPercent = constructionCompletionPercent.Value;
        if (constructionLicenseExpirationDate.HasValue) ConstructionLicenseExpirationDate = constructionLicenseExpirationDate.Value;
        if (isAppraisable.HasValue) IsAppraisable = isAppraisable.Value;

        // Building Info
        if (buildingType is not null) BuildingType = buildingType;
        if (buildingTypeOther is not null) BuildingTypeOther = buildingTypeOther;
        if (numberOfFloors.HasValue) NumberOfFloors = numberOfFloors.Value;
        if (decorationType is not null) DecorationType = decorationType;
        if (decorationTypeOther is not null) DecorationTypeOther = decorationTypeOther;
        if (isEncroached.HasValue) IsEncroached = isEncroached.Value;
        if (encroachmentRemark is not null) EncroachmentRemark = encroachmentRemark;
        if (encroachmentArea.HasValue) EncroachmentArea = encroachmentArea.Value;

        // Construction Details
        if (buildingMaterial is not null) BuildingMaterial = buildingMaterial;
        if (buildingStyle is not null) BuildingStyle = buildingStyle;
        if (isResidential.HasValue) IsResidential = isResidential.Value;
        if (buildingAge.HasValue) BuildingAge = buildingAge.Value;
        if (constructionYear.HasValue) ConstructionYear = constructionYear.Value;
        if (isResidentialRemark is not null) IsResidentialRemark = isResidentialRemark;
        if (constructionStyleType is not null) ConstructionStyleType = constructionStyleType;
        if (constructionStyleRemark is not null) ConstructionStyleRemark = constructionStyleRemark;

        // Structure Components
        if (structureType is not null) StructureType = structureType;
        if (structureTypeOther is not null) StructureTypeOther = structureTypeOther;
        if (roofFrameType is not null) RoofFrameType = roofFrameType;
        if (roofFrameTypeOther is not null) RoofFrameTypeOther = roofFrameTypeOther;
        if (roofType is not null) RoofType = roofType;
        if (roofTypeOther is not null) RoofTypeOther = roofTypeOther;
        if (ceilingType is not null) CeilingType = ceilingType;
        if (ceilingTypeOther is not null) CeilingTypeOther = ceilingTypeOther;
        if (interiorWallType is not null) InteriorWallType = interiorWallType;
        if (interiorWallTypeOther is not null) InteriorWallTypeOther = interiorWallTypeOther;
        if (exteriorWallType is not null) ExteriorWallType = exteriorWallType;
        if (exteriorWallTypeOther is not null) ExteriorWallTypeOther = exteriorWallTypeOther;
        if (fenceType is not null) FenceType = fenceType;
        if (fenceTypeOther is not null) FenceTypeOther = fenceTypeOther;
        if (constructionType is not null) ConstructionType = constructionType;
        if (constructionTypeOther is not null) ConstructionTypeOther = constructionTypeOther;

        // Utilization
        if (utilizationType is not null) UtilizationType = utilizationType;
        if (otherPurposeUsage is not null) OtherPurposeUsage = otherPurposeUsage;

        // Area & Pricing
        if (totalBuildingArea.HasValue) TotalBuildingArea = totalBuildingArea.Value;
        if (buildingInsurancePrice.HasValue) BuildingInsurancePrice = buildingInsurancePrice.Value;
        if (sellingPrice.HasValue) SellingPrice = sellingPrice.Value;
        if (forcedSalePrice.HasValue) ForcedSalePrice = forcedSalePrice.Value;

        // Other
        if (remark is not null) Remark = remark;
    }
}
