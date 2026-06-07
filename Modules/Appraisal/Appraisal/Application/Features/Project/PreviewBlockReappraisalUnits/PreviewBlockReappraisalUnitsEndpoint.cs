namespace Appraisal.Application.Features.Project.PreviewBlockReappraisalUnits;

/// <summary>
/// Dry-run preview endpoint for the block-reappraisal Excel reconcile flow.
/// Returns per-unit status classification (Sold / NewlySold / Available / MatchDifference)
/// and a summary count without writing anything to the database.
/// The caller should show the results in a confirm dialog before calling the apply endpoint.
/// </summary>
public class PreviewBlockReappraisalUnitsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/project/units/reappraisal-preview",
                async (
                    Guid appraisalId,
                    IFormFile file,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var extension = Path.GetExtension(file.FileName);
                    if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
                    {
                        return Results.Problem(
                            title: "Invalid file type",
                            detail: "Only .xlsx files are allowed.",
                            statusCode: StatusCodes.Status400BadRequest);
                    }

                    const long maxFileSize = 10 * 1024 * 1024; // 10 MB
                    if (file.Length > maxFileSize)
                    {
                        return Results.Problem(
                            title: "File too large",
                            detail: "File size must not exceed 10 MB.",
                            statusCode: StatusCodes.Status400BadRequest);
                    }

                    using var stream = file.OpenReadStream();

                    var command = new PreviewBlockReappraisalUnitsCommand(
                        appraisalId, stream, file.FileName);

                    var result = await sender.Send(command, cancellationToken);

                    return Results.Ok(result);
                }
            )
            .WithName("PreviewBlockReappraisalUnits")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<PreviewBlockReappraisalUnitsResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Dry-run preview for block reappraisal Excel reconcile")
            .WithDescription(
                "Parses the Excel and classifies each working-copy unit into Sold / NewlySold / Available / MatchDifference " +
                "without writing to the database. Call this before the apply endpoint to show a confirm dialog.")
            .WithTags("Project")
            .RequireAuthorization()
            .DisableAntiforgery();
    }
}
