namespace Appraisal.Application.Features.Appraisals.UpdateBuildingProperty;

/// <summary>
/// Request to update a building property detail
/// </summary>
public record UpdateBuildingPropertyRequest(
    // Property Identification
    string? PropertyName = null,
    string? BuildingNumber = null,
    string? ModelName = null,
    string? BuiltOnTitleNumber = null,
    // Owner
    string? OwnerName = null,
    bool? IsOwnerVerified = null,
    string? HouseNumber = null,
    // Building Status
    string? BuildingConditionType = null,
    bool? IsUnderConstruction = null,
    decimal? ConstructionCompletionPercent = null,
    DateTime? ConstructionLicenseExpirationDate = null,
    bool? IsAppraisable = null,
    bool? HasObligation = null,
    string? ObligationDetails = null,
    // Building Info
    string? BuildingType = null,
    string? BuildingTypeOther = null,
    int? NumberOfFloors = null,
    string? DecorationType = null,
    string? DecorationTypeOther = null,
    bool? IsEncroachingOthers = null,
    string? EncroachingOthersRemark = null,
    decimal? EncroachingOthersArea = null,
    // Construction Details
    string? BuildingMaterialType = null,
    string? BuildingStyleType = null,
    bool? IsResidential = null,
    int? BuildingAge = null,
    int? ConstructionYear = null,
    string? ResidentialRemark = null,
    string? ConstructionStyleType = null,
    string? ConstructionStyleRemark = null,
    // Structure Components
    List<string>? StructureType = null,
    string? StructureTypeOther = null,
    List<string>? RoofFrameType = null,
    string? RoofFrameTypeOther = null,
    List<string>? RoofType = null,
    string? RoofTypeOther = null,
    List<string>? CeilingType = null,
    string? CeilingTypeOther = null,
    List<string>? InteriorWallType = null,
    string? InteriorWallTypeOther = null,
    List<string>? ExteriorWallType = null,
    string? ExteriorWallTypeOther = null,
    List<string>? FenceType = null,
    string? FenceTypeOther = null,
    string? ConstructionType = null,
    string? ConstructionTypeOther = null,
    // Utilization
    string? UtilizationType = null,
    string? UtilizationTypeOther = null,
    // Area & Pricing
    decimal? TotalBuildingArea = null,
    decimal? BuildingInsurancePrice = null,
    decimal? SellingPrice = null,
    decimal? ForcedSalePrice = null,
    // Other
    string? Remark = null
);
