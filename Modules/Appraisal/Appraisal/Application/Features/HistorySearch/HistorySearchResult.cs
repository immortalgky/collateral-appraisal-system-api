namespace Appraisal.Application.Features.HistorySearch;

/// <summary>
/// Top-level response for a history search.
/// Both collections are always present; Appraisals is empty for external users.
/// </summary>
public record HistorySearchResult(
    PaginatedResult<AppraisalPinDto> Appraisals,
    PaginatedResult<MarketComparablePinDto> MarketComparables);

/// <summary>
/// Green pin — one per completed Appraisal application that has at least one located property.
/// Representative location is chosen as the nearest property when a centre is given,
/// otherwise the lowest-sequence-number property with a non-null GeoPoint.
/// Returned for internal users only.
/// </summary>
public record AppraisalPinDto(
    Guid AppraisalId,
    string? AppraisalNumber,
    decimal Lat,
    decimal Lon,
    /// <summary>Collateral type code of the representative property (e.g. L, LB, U).</summary>
    string? PropertyType,
    /// <summary>Building type of the representative property; null for non-building collaterals.</summary>
    string? BuildingType,
    decimal? AppraisedValue,
    DateTime? AppraisedDate,
    /// <summary>Distance from the search centre in km. Null when no centre point was supplied.</summary>
    double? DistanceKm,
    string? Province,
    string? District,
    string? SubDistrict,
    string? CustomerName);

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
    /// <summary>Appraisal report number of the most recent appraisal this comparable was used in.</summary>
    string? AppraisalNumber,
    /// <summary>Customer name of that same most-recent linked appraisal; null if the comparable isn't linked to any.</summary>
    string? CustomerName,
    /// <summary>Appraisal date (appointment date) of that same linked appraisal; null if not linked. Used as the row's "Appraisal Date".</summary>
    DateTime? AppraisalDate);
