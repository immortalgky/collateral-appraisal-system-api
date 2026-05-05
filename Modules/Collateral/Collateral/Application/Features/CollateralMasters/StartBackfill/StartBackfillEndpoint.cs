using Collateral.CollateralMasters.Services;
using Shared.Identity;

namespace Collateral.Application.Features.CollateralMasters.StartBackfill;

/// <summary>
/// POST /collateral-masters/admin/backfill
/// Admin-only. Kicks off the one-shot backfill job in the background.
/// Returns JobId + StartedAt immediately without waiting for the job to finish.
/// </summary>
public class StartBackfillEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/collateral-masters/admin/backfill",
                (
                    CollateralBackfillJob job,
                    ICurrentUserService currentUser,
                    CancellationToken cancellationToken
                ) =>
                {
                    if (!currentUser.IsInRole("Admin") && !currentUser.IsInRole("IntAdmin"))
                        throw new UnauthorizedAccessException("Only Admin users can trigger the backfill job.");

                    var jobId = job.StartAsync(cancellationToken);
                    var startedAt = DateTime.UtcNow;

                    return Results.Ok(new StartBackfillResponse(jobId, startedAt));
                }
            )
            .WithName("StartCollateralBackfill")
            .Produces<StartBackfillResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Start collateral backfill job (admin)")
            .WithDescription("Kicks off the one-shot backfill of historical completed appraisals. Returns job id immediately.")
            .WithTags("CollateralMaster")
            .RequireAuthorization();
    }
}

public record StartBackfillResponse(Guid JobId, DateTime StartedAt);
