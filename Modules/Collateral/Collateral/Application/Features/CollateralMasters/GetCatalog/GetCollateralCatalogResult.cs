namespace Collateral.Application.Features.CollateralMasters.GetCatalog;

public record GetCollateralCatalogResult(PaginatedResult<CollateralCatalogItemDto> Items);

/// <summary>
/// Minimal flat projection for the catalog list.
/// Type-specific key fields are projected from the aliased view columns.
/// </summary>
public record CollateralCatalogItemDto(
    Guid Id,
    string CollateralType,
    string? OwnerName,
    DateTime CreatedAt,
    int EngagementCount,
    DateTime? LastAppraisedDate,
    decimal? LastAppraisedValue,
    // Land — province for grouping/filtering
    string? Land_Province,
    bool? IsUnderConstructionAtLastAppraisal,
    decimal? OverallConstructionProgressPercent,
    // Condo — province for grouping/filtering
    string? Condo_Province
);
