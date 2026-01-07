namespace Appraisal.Application.Features.Appraisals.GetBuildingProperty;

/// <summary>
/// Result of getting a building property
/// </summary>
public record GetBuildingPropertyResult(
    // Property
    Guid PropertyId,
    Guid AppraisalId,
    int SequenceNumber,
    string PropertyType,
    string? Description,
    // Detail
    Guid DetailId,
    // Property Identification
    string? PropertyName,
    string? BuildingNumber,
    string? ModelName,
    string? BuiltOnTitleNumber,
    // Owner
    string? OwnerName,
    bool IsOwnerVerified,
    string? HouseNumber,
    // Building Status
    string? BuildingCondition,
    bool IsUnderConstruction,
    decimal? ConstructionCompletionPercent,
    DateTime? ConstructionLicenseExpirationDate,
    bool IsAppraisable,
    bool HasObligation,
    string? ObligationDetails,
    // Building Info
    string? BuildingType,
    string? BuildingTypeOther,
    int? NumberOfFloors,
    string? DecorationType,
    string? DecorationTypeOther,
    bool IsEncroached,
    string? EncroachmentRemark,
    decimal? EncroachmentArea,
    // Construction Details
    string? BuildingMaterial,
    string? BuildingStyle,
    bool IsResidential,
    int? BuildingAge,
    int? ConstructionYear,
    string? IsResidentialRemark,
    string? ConstructionStyleType,
    string? ConstructionStyleRemark,
    // Structure Components
    string? StructureType,
    string? StructureTypeOther,
    string? RoofFrameType,
    string? RoofFrameTypeOther,
    string? RoofType,
    string? RoofTypeOther,
    string? CeilingType,
    string? CeilingTypeOther,
    string? InteriorWallType,
    string? InteriorWallTypeOther,
    string? ExteriorWallType,
    string? ExteriorWallTypeOther,
    string? FenceType,
    string? FenceTypeOther,
    string? ConstructionType,
    string? ConstructionTypeOther,
    // Utilization
    string? UtilizationType,
    string? OtherPurposeUsage,
    // Area & Pricing
    decimal? TotalBuildingArea,
    decimal? BuildingInsurancePrice,
    decimal? SellingPrice,
    decimal? ForcedSalePrice,
    // Other
    string? Remark
);
