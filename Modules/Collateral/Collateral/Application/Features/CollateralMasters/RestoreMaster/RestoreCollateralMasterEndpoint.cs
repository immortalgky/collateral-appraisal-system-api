namespace Collateral.Application.Features.CollateralMasters.RestoreMaster;

/// <summary>
/// POST /collateral-masters/{id}/restore
/// Admin-only. Clears IsDeleted on master and detail.
/// Rejects with 409 if restoring would create a dedup-key collision.
/// </summary>
public class RestoreCollateralMasterEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/collateral-masters/{id}/restore",
                async (
                    Guid id,
                    RestoreCollateralMasterRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new RestoreCollateralMasterCommand(id, request.Reason);
                    var result = await sender.Send(command, cancellationToken);
                    return Results.Ok(result);
                }
            )
            .WithName("RestoreCollateralMaster")
            .Produces<RestoreCollateralMasterResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Restore a soft-deleted collateral master (admin)")
            .WithDescription("Clears IsDeleted. Rejects with 409 if another master has the same dedup key.")
            .WithTags("CollateralMaster")
            .RequireAuthorization();
    }
}
