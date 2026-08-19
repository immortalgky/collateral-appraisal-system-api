using Carter;
using Integration.FileInterface.Jobs.HostLink;
using Shared.Identity;

namespace Api.Endpoints.As400Legacy;

/// <summary>
/// POST /collateral-masters/admin/import-as400-legacy — one-shot, admin-only.
///
/// Brings the AS400 legacy collateral listing into the collateral store. Fire-and-forget like the
/// collateral backfill: the caller gets a job id immediately and polls for the outcome.
///
/// <b>Run it after the link ingest.</b> Whether a legacy row attaches to a master we already know or
/// mints a new one depends on <c>CollateralMasters.HostCollateralId</c>, which the nightly link feed
/// populates. Import first and every already-known collateral is duplicated as a fresh master.
/// </summary>
public class StartAs400LegacyImportEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/collateral-masters/admin/import-as400-legacy",
                (As400LegacyImportJob job, ICurrentUserService currentUser) =>
                {
                    if (!currentUser.IsInRole("Admin") && !currentUser.IsInRole("IntAdmin"))
                        throw new UnauthorizedAccessException(
                            "Only Admin users can run the AS400 legacy import.");

                    return Results.Ok(new StartAs400LegacyImportResponse(job.Start()));
                })
            .WithName("StartAs400LegacyImport")
            .Produces<StartAs400LegacyImportResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Import the AS400 legacy collateral listing (admin)")
            .WithDescription(
                "Registers collateral the bank held before this system existed. Run AFTER the "
                + "host-collateral-link ingest. Returns a job id immediately.")
            .WithTags("CollateralMaster")
            .RequireAuthorization();

        app.MapGet(
                "/collateral-masters/admin/import-as400-legacy/{jobId:guid}",
                (Guid jobId, As400LegacyImportJob job, ICurrentUserService currentUser) =>
                {
                    if (!currentUser.IsInRole("Admin") && !currentUser.IsInRole("IntAdmin"))
                        throw new UnauthorizedAccessException(
                            "Only Admin users can read the AS400 legacy import status.");

                    var status = job.GetStatus(jobId);
                    return status is null ? Results.NotFound() : Results.Ok(status);
                })
            .WithName("GetAs400LegacyImportStatus")
            .Produces<As400LegacyImportStatus>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("AS400 legacy import status (admin)")
            .WithTags("CollateralMaster")
            .RequireAuthorization();
    }
}

public record StartAs400LegacyImportResponse(Guid JobId);
