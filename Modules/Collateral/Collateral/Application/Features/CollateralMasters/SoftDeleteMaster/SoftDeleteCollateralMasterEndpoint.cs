namespace Collateral.Application.Features.CollateralMasters.SoftDeleteMaster;

/// <summary>
/// DELETE /collateral-masters/{id}
/// Admin-only. Soft-deletes the master and its type-specific detail (IsDeleted=1).
/// Engagements are preserved. Rejects with 409 if active Leasehold references exist.
/// </summary>
public class SoftDeleteCollateralMasterEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/collateral-masters/{id}",
                async (
                    Guid id,
                    string reason,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new SoftDeleteCollateralMasterCommand(id, reason ?? "");
                    var result = await sender.Send(command, cancellationToken);
                    return Results.Ok(result);
                }
            )
            .WithName("SoftDeleteCollateralMaster")
            .Produces<SoftDeleteCollateralMasterResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Soft-delete a collateral master (admin)")
            .WithDescription("Sets IsDeleted=1 on master and detail. Preserves engagements. Blocked by active Leasehold references. Pass `reason` as a query parameter.")
            .WithTags("CollateralMaster")
            .RequireAuthorization();
    }
}
