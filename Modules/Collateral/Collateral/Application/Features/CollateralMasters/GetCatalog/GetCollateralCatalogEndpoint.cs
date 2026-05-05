namespace Collateral.Application.Features.CollateralMasters.GetCatalog;

/// <summary>
/// GET /collateral-masters?type=&amp;province=&amp;owner=&amp;isUnderConstruction=&amp;minAppraisals=&amp;lastAppraisedFrom=&amp;lastAppraisedTo=&amp;page=&amp;pageSize=&amp;sort=
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
                    string? type,
                    string? province,
                    string? owner,
                    bool? isUnderConstruction,
                    int? minAppraisals,
                    DateOnly? lastAppraisedFrom,
                    DateOnly? lastAppraisedTo,
                    string? sort,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetCollateralCatalogQuery(
                        paginationRequest,
                        type, province, owner, isUnderConstruction, minAppraisals,
                        lastAppraisedFrom, lastAppraisedTo, sort);

                    var result = await sender.Send(query, cancellationToken);

                    return Results.Ok(result);
                }
            )
            .WithName("GetCollateralCatalog")
            .Produces<GetCollateralCatalogResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Get paginated collateral master catalog")
            .WithDescription("Returns a paginated, filtered list of collateral masters. Admin-only.")
            .WithTags("CollateralMaster")
            .RequireAuthorization();
    }
}
