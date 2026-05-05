namespace Collateral.Application.Features.CollateralMasters.ReplayAppraisal;

/// <summary>
/// POST /collateral-masters/admin/replay/{appraisalId}
/// Admin-only. Synchronously replays the upsert for a single appraisal.
/// Writes a BackfillReport row and returns the result inline.
/// </summary>
public class ReplayAppraisalEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/collateral-masters/admin/replay/{appraisalId}",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new ReplayAppraisalCommand(appraisalId);
                    var result = await sender.Send(command, cancellationToken);
                    return Results.Ok(result);
                }
            )
            .WithName("ReplayCollateralAppraisal")
            .Produces<ReplayAppraisalResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Replay collateral upsert for a single appraisal (admin)")
            .WithDescription("Re-runs ProcessAppraisalAsync for the given appraisalId. Use after correcting upstream data.")
            .WithTags("CollateralMaster")
            .RequireAuthorization();
    }
}
