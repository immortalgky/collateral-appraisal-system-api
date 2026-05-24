namespace Collateral.Application.Features.CollateralMasters.GetCatalog;

public record GetCollateralCatalogResult(PaginatedResult<CollateralCatalogItemDto> Items);

/// <summary>
/// Minimal flat projection for the catalog list.
/// Type-specific key fields are projected from the aliased view columns.
/// Phase 1 adds: Land/Condo title numbers, district/subDistrict, condoName, lat/lng.
/// </summary>
// Constructor parameter order MUST match the SELECT column order in
// GetCollateralCatalogQueryHandler.SelectColumns — Dapper materializes positional
// records ordinally, not by name. If you add/move columns there, mirror here.
public record CollateralCatalogItemDto(
    Guid Id,
    string CollateralType,
    string? OwnerName,
    DateTime CreatedAt,
    int EngagementCount,
    DateTime? LastAppraisedDate,
    decimal? LastAppraisedValue,
    // Land identity (matches SELECT order)
    string? Land_Province,
    string? Land_District,
    string? Land_SubDistrict,
    string? Land_TitleNumber,
    bool? IsUnderConstructionAtLastAppraisal,
    decimal? OverallConstructionProgressPercent,
    // Condo identity (matches SELECT order)
    string? Condo_Province,
    string? Condo_TitleNumber,
    string? Condo_CondoName,
    decimal? Condo_Latitude,
    decimal? Condo_Longitude,
    // Land geo (SELECT places these after Condo geo)
    decimal? Land_Latitude,
    decimal? Land_Longitude
);
