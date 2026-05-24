namespace Collateral.Application.Features.CollateralMasters.GetCollateralEngagementDetail;

/// <summary>
/// GET /collateral-masters/{id}/engagements/{engagementId}/detail
///
/// Returns the structured detail for a collateral engagement on the History Search detail screen:
///  - Round meta (appraisal number, date, type, value).
///  - Identity of the clicked collateral master.
///  - All appraisal properties grouped by group number.
///
/// Internal-only — mirrors the history-search.view policy used by HistorySearchEndpoint and
/// SearchCollateralEngagementsEndpoint (green-pin / collateral data is never exposed to externals).
/// </summary>
public class GetCollateralEngagementDetailEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/collateral-masters/{id:guid}/engagements/{engagementId:guid}/detail",
                async (
                    Guid id,
                    Guid engagementId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetCollateralEngagementDetailQuery(id, engagementId);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result);
                }
            )
            .WithName("GetCollateralEngagementDetail")
            .Produces<GetCollateralEngagementDetailResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Get collateral engagement detail")
            .WithDescription(
                "Returns round meta, clicked-collateral identity, and grouped appraisal properties " +
                "for the History Search detail screen. Internal users only.")
            .WithTags("CollateralMaster")
            .RequireAuthorization("history-search.view");
    }
}
