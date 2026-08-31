using Carter;
using Integration.FileInterface.Jobs.CollateralResult;
using Shared.Identity;

namespace Api.Endpoints.FileInterfaces;

/// <summary>
/// GET /file-interfaces/admin/collateral-result/preview — builds the next outbound file without
/// sending it or marking anything sent.
///
/// Exists for the cut-over: the record grows from 208 to 231 characters and the collateral id is now
/// resolved by walking the appraisal chain instead of read off a CollateralMaster. The file has to be
/// diffed against the old output on the same data before a switch-over date is agreed with AS400, and
/// triggering the real job to obtain a file would consume the ledger.
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
