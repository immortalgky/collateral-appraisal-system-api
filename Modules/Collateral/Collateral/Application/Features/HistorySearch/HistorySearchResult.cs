namespace Collateral.Application.Features.HistorySearch;

/// <summary>
/// Top-level response for a history search.
/// Both collections are always present; Collateral is empty for external users.
/// </summary>
public record HistorySearchResult(
    PaginatedResult<CollateralPinDto> Collateral,
    PaginatedResult<MarketComparablePinDto> MarketComparables);

/// <summary>
/// Green pin — one per CollateralMaster (Land type) with engagement summary.
/// Returned for internal users only.
/// </summary>
public record CollateralPinDto(
    Guid CollateralMasterId,
    decimal Lat,
    decimal Lon,
    string CollateralType,
    string? PropertyType,
    int EngagementCount,
    DateTime? LastAppraisedDate,
    decimal? LastAppraisedValue,
    /// <summary>Distance from the search centre in km. Null when no centre point was supplied.</summary>
    double? DistanceKm,
    string? Province,
    string? District,
    string? SubDistrict,
    /// <summary>Latest appraisal report number for this collateral (LandDetail.LastAppraisalNumber).</summary>
    string? LastAppraisalNumber);

/// <summary>
/// Blue pin — one per MarketComparable that has geo-coordinates.
/// External users see only their own company's records.
/// </summary>
public record MarketComparablePinDto(
    Guid MarketComparableId,
    decimal Lat,
    decimal Lon,
    string PropertyType,
    string SurveyName,
    DateTime? InfoDateTime,
    decimal? OfferPrice,
    decimal? SalePrice,
    /// <summary>Distance from the search centre in km. Null when no centre point was supplied.</summary>
    double? DistanceKm,
    /// <summary>Appraisal report number of the most recent appraisal this comparable was used in
    /// (via appraisal.AppraisalComparables). Null if the comparable isn't linked to any appraisal.</summary>
    string? AppraisalNumber);
