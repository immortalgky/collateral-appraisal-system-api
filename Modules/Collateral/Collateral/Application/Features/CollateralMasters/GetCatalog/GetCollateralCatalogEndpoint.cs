namespace Collateral.Application.Features.CollateralMasters.GetCatalog;

/// <summary>
/// GET /collateral-masters
/// Supports all existing filters plus Phase 1 additions:
///   type (multi-select: ?type=Land&amp;type=Condo)
///   titleNumber (LIKE — Land only; Condo has no title number)
///   district, subDistrict (exact)
///   companyId (masters with at least one engagement by this company)
///   q (free-text: owner, titleNumber, address, condoName)
///   centerLat, centerLng, radiusKm (geo-bound circle filter)
/// Admin-only.
/// </summary>
public class GetCollateralCatalogEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/collateral-masters",
                async (
                    [AsParameters] PaginationRequest paginationRequest,
                    // Multi-select type: ?type=Land&type=Condo
                    string[]? type,
                    string? province,
                    string? owner,
                    bool? isUnderConstruction,
                    int? minAppraisals,
                    DateOnly? lastAppraisedFrom,
                    DateOnly? lastAppraisedTo,
                    string? sort,
                    // Phase 1 new filters
                    string? titleNumber,
                    string? district,
                    string? subDistrict,
                    string? companyId,
                    string? q,
                    decimal? centerLat,
                    decimal? centerLng,
                    decimal? radiusKm,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetCollateralCatalogQuery(
                        paginationRequest,
                        Types: type,
                        Province: province,
                        Owner: owner,
                        IsUnderConstruction: isUnderConstruction,
                        MinAppraisals: minAppraisals,
                        LastAppraisedFrom: lastAppraisedFrom,
                        LastAppraisedTo: lastAppraisedTo,
                        Sort: sort,
                        TitleNumber: titleNumber,
                        District: district,
                        SubDistrict: subDistrict,
                        CompanyId: companyId,
                        Q: q,
                        CenterLat: centerLat,
                        CenterLng: centerLng,
                        RadiusKm: radiusKm);

                    var result = await sender.Send(query, cancellationToken);

                    return Results.Ok(result.Items);
                }
            )
            .WithName("GetCollateralCatalog")
            .Produces<PaginatedResult<CollateralCatalogItemDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Get paginated collateral master catalog")
            .WithDescription("Returns a paginated, filtered list of collateral masters. Admin-only.")
            .WithTags("CollateralMaster")
            .RequireAuthorization();
    }
}
