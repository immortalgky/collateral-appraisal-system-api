namespace Collateral.Application.Features.CollateralMasters.GetEngagementSnapshot;

/// <summary>
/// GET /collateral-masters/{id}/engagements/{engagementId}
/// Authenticated. Returns the full engagement row including Snapshot JSON.
/// </summary>
public class GetEngagementSnapshotEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/collateral-masters/{id:guid}/engagements/{engagementId:guid}",
                async (
                    Guid id,
                    Guid engagementId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetEngagementSnapshotQuery(id, engagementId);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result);
                }
            )
            .WithName("GetCollateralEngagementSnapshot")
            .Produces<GetEngagementSnapshotResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Get engagement snapshot")
            .WithDescription("Returns the full engagement row including the raw Snapshot JSON for a specific collateral master engagement.")
            .WithTags("CollateralMaster")
            .RequireAuthorization();
    }
}
