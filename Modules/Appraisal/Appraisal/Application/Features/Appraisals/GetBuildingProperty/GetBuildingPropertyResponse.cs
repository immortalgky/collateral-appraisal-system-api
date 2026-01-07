namespace Appraisal.Application.Features.Appraisals.GetBuildingProperty;

/// <summary>
/// Response for getting a building property
/// </summary>
public record GetBuildingPropertyResponse(
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
    string? BuildingNo,
    string? ModelName,
    string? BuiltOnTitleNo,
    // Owner
    string Owner,
    bool VerifiableOwner,
    string? HouseNumber,
    // Building Status
    string? BuildingCondition,
    bool UnderConstruction,
    decimal? ConstCompletionPct,
    DateTime? LicenseExpirationDate,
    bool IsAppraise,
    bool IsObligation,
    string? Obligation,
    // Building Info
    string? BuildingType,
    string? BuildingTypeOther,
    int? TotalFloor,
    string? Decoration,
    string? DecorationOther,
    bool IsEncroached,
    string? IsEncroachedRemark,
    decimal? EncroachArea,
    // Construction Details
    string? BuildingMaterial,
    string? BuildingStyle,
    bool IsResidential,
    int? BuildingYear,
    string? DueTo,
    string? ConstStyle,
    string? ConstStyleRemark,
    // Structure Components
    string? GeneralStructure,
    string? GeneralStructureOther,
    string? RoofFrame,
    string? RoofFrameOther,
    string? Roof,
    string? RoofOther,
    string? Ceiling,
    string? CeilingOther,
    string? InteriorWall,
    string? InteriorWallOther,
    string? ExteriorWall,
    string? ExteriorWallOther,
    string? Fence,
    string? FenceOther,
    string? ConstType,
    string? ConstTypeOther,
    // Utilization
    string? Utilization,
    string? UseForOtherPurpose,
    // Area & Pricing
    decimal? BuildingArea,
    decimal? BuildingInsurancePrice,
    decimal? SellingPrice,
    decimal? ForceSellingPrice,
    // Other
    string? Remark
);
