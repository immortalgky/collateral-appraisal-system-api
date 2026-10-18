using Carter;
using Integration.FileInterface.Jobs.CollateralResult;
using Shared.Identity;

namespace Api.Endpoints.FileInterfaces;

/// <summary>
/// GET /file-interfaces/admin/collateral-result/preview — builds the next outbound file without
/// sending it or marking anything sent.
///
/// Shows the file for the same rows, in the same order, as the next run would send, so it can be
/// inspected without consuming the ledger (triggering the real job to obtain a file would mark
/// everything sent). It can differ from the delivered file: the header date is the preview day while the
/// job stamps its own run date; with 0 rows the preview still returns a header and trailer but the job
/// sends nothing; and the preview ignores the config's IsActive.
/// </summary>
public class PreviewCollateralResultEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/file-interfaces/admin/collateral-result/preview",
                async (
                    CollateralResultExportJob job,
                    ICurrentUserService currentUser,
                    CancellationToken cancellationToken) =>
                {
                    if (!currentUser.IsInRole("Admin") && !currentUser.IsInRole("IntAdmin"))
                        throw new UnauthorizedAccessException(
                            "Only Admin users can preview the collateral result file.");

                    var preview = await job.BuildPreviewAsync(cancellationToken);
                    return Results.Ok(preview);
                })
            .WithName("PreviewCollateralResult")
            .Produces<CollateralResultPreview>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Preview the outbound collateral result file (admin)")
            .WithDescription(
                "Builds the file the next run would send and returns it as text. Writes nothing and "
                + "marks nothing as sent.")
            .WithTags("FileInterface")
            .RequireAuthorization();
    }
}
