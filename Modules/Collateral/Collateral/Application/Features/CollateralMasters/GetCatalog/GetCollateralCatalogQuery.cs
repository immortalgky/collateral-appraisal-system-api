namespace Collateral.Application.Features.CollateralMasters.GetCatalog;

/// <summary>
/// Paginated catalog of CollateralMasters. Admin-only.
/// </summary>
public record GetCollateralCatalogQuery(
    PaginationRequest PaginationRequest,
    // Existing filters
    string[]? Types,           // multi-select: ?type=Land&type=Condo (was single string)
    string? Province,
    string? Owner,
    bool? IsUnderConstruction,
    int? MinAppraisals,
    DateOnly? LastAppraisedFrom,
    DateOnly? LastAppraisedTo,
    string? Sort,
    // Phase 1 — new filters
    string? TitleNumber,       // LIKE against Land title number AND Condo TitleNumber
    string? District,          // exact match against Land_District
    string? SubDistrict,       // exact match against Land_SubDistrict
    string? CompanyId,         // masters that have at least one engagement with this company
    string? Q,                 // free-text across owner, titleNumber, address, condoName
    // Geo-bound circle filter (requires both Land and Condo lat/lng columns in view)
    decimal? CenterLat,
    decimal? CenterLng,
    decimal? RadiusKm
) : IQuery<GetCollateralCatalogResult>;
