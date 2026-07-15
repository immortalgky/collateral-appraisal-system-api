using Collateral.CollateralMasters.Services;
using Shared.Identity;
using Shared.Time;

namespace Collateral.Application.Features.CollateralMasters.BackfillHostCollateralId;

/// <summary>
/// POST /collateral-masters/admin/backfill-host-collateral-id
/// Admin-only. Kicks off the standalone host-collateral-id backfill in the background.
/// Returns JobId + StartedAt immediately without waiting for the job to finish.
/// </summary>
public class BackfillHostCollateralIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/collateral-masters/admin/backfill-host-collateral-id",
                (
                    HostCollateralIdBackfillJob job,
                    ICurrentUserService currentUser,
                    IDateTimeProvider dateTimeProvider
                ) =>
                {
                    if (!currentUser.IsInRole("Admin") && !currentUser.IsInRole("IntAdmin"))
                        throw new UnauthorizedAccessException(
                            "Only Admin users can trigger the host-collateral-id backfill job.");

                    // Do not pass the request CancellationToken — this is fire-and-forget and the
                    // request token cancels as soon as the response returns. The job runs under the
                    // host's ApplicationStopping token instead (see HostCollateralIdBackfillJob.StartAsync).
                    var jobId = job.StartAsync();
                    var startedAt = dateTimeProvider.ApplicationNow;

                    return Results.Ok(new BackfillHostCollateralIdResponse(jobId, startedAt));
                }
            )
            .WithName("StartHostCollateralIdBackfill")
            .Produces<BackfillHostCollateralIdResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Start host-collateral-id backfill job (admin)")
            .WithDescription(
                "Copies the AS400 HostCollateralId already stamped on appraisal source rows onto the owning "
                + "CollateralMaster (one per appraisal). Idempotent. Returns job id immediately.")
            .WithTags("CollateralMaster")
            .RequireAuthorization();
    }
}

public record BackfillHostCollateralIdResponse(Guid JobId, DateTime StartedAt);
