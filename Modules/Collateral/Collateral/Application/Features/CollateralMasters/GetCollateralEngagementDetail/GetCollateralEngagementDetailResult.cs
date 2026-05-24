namespace Collateral.Application.Features.CollateralMasters.GetCollateralEngagementDetail;

/// <summary>
/// Root response for the collateral engagement detail screen.
/// Returns round meta, the identity of the clicked collateral, and all appraisal
/// properties grouped by group for the full-picture view.
/// </summary>
public record GetCollateralEngagementDetailResult(
    RoundMetaDto Meta,
    CollateralIdentityDto CollateralIdentity,
    IReadOnlyList<PropertyGroupDto> Groups
);

/// <summary>
/// Appraisal-round metadata derived from the CollateralEngagement row.
/// </summary>
public record RoundMetaDto(
    Guid EngagementId,
    Guid AppraisalId,
    string? AppraisalNumber,
    DateTime? AppraisalDate,
    string? AppraisalType,
    /// <summary>The appraised value stored on the engagement row (AppraisedValue column).</summary>
    decimal? AppraisalValue
);

/// <summary>
/// Identity fields for the CLICKED collateral master, derived from the matched
/// AppraisalPropertyForCollateral in the same appraisal.
/// All code fields (CollateralType, BuildingTypeCode) are codes — the FE resolves labels.
/// </summary>
public record CollateralIdentityDto(
    /// <summary>PropertyTypeCode, e.g. "L", "LB", "U", "LSL", "M".</summary>
    string? CollateralType,
    /// <summary>BuildingTypeCode from a Building property in the same group (null if none).</summary>
    string? BuildingTypeCode,
    /// <summary>Project or village name: Condo → CondoName, Land/LB → Village.</summary>
    string? ProjectOrVillageName,
    /// <summary>Free-text street (Land/LB only). The FE prepends this to the resolved address names.</summary>
    string? Street,
    /// <summary>Sub-district CODE (6-digit). FE resolves the name. Land/LB only.</summary>
    string? SubDistrict,
    /// <summary>District CODE (4-digit). FE resolves the name. Land/LB only.</summary>
    string? District,
    /// <summary>Province CODE (2-digit). FE resolves the name. Land/LB + Condo.</summary>
    string? Province,
    decimal? Latitude,
    decimal? Longitude,
    /// <summary>Land area in sq.wa (LandIdentity.LandArea). Null for Condo/Machine/Leasehold.</summary>
    decimal? LandAreaInSqWa,
    /// <summary>Land/LB → building area (sq.m) from BuildingIdentity; Condo → UsableArea; else null.</summary>
    decimal? BuildingOrUsableArea,
    /// <summary>ModelName for Condo properties; null otherwise.</summary>
    string? ModelName
);

/// <summary>
/// A group of appraisal properties sharing the same GroupNumber.
/// </summary>
public record PropertyGroupDto(
    int GroupNumber,
    IReadOnlyList<PropertySummaryDto> Properties
);

/// <summary>
/// Summary row for a single property within a group.
/// </summary>
public record PropertySummaryDto(
    Guid PropertyId,
    /// <summary>Best available identifier: first title number / condo unit info / registration number.</summary>
    string? Name,
    /// <summary>PropertyTypeCode, e.g. "L", "LB", "U", "LSL", "M", "B".</summary>
    string? CollateralType,
    /// <summary>Land area (sq.wa) or usable/building area (sq.m) depending on type.</summary>
    decimal? Area,
    decimal? Latitude,
    decimal? Longitude
);
