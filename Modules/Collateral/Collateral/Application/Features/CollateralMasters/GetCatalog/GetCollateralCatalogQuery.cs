namespace Collateral.Application.Features.CollateralMasters.GetCatalog;

/// <summary>
/// Paginated catalog of CollateralMasters. Admin-only.
/// </summary>
public record GetCollateralCatalogQuery(
    PaginationRequest PaginationRequest,
    string? Type,
    string? Province,
    string? Owner,
    bool? IsUnderConstruction,
    int? MinAppraisals,
    DateOnly? LastAppraisedFrom,
    DateOnly? LastAppraisedTo,
    string? Sort
) : IQuery<GetCollateralCatalogResult>;
