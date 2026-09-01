namespace Appraisal.Application.Features.Project.UploadBlockReappraisalUnits;

/// <summary>
/// Endpoint for the block reappraisal units Excel re-match operation.
/// Accepts the same .xlsx format as UploadProjectUnits but performs a non-destructive
/// match: existing units absent from the Excel are auto-marked sold; units present are
/// confirmed unsold. Attribute changes and rows the Excel adds are applied only when the caller
/// passes confirmUpdates=true, having reviewed them through the preview endpoint.
/// </summary>
public class UploadBlockReappraisalUnitsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/project/units/reappraisal-upload",
                async (
                    Guid appraisalId,
                    IFormFile file,
                    // Query string, not the multipart body: a bare bool?/Guid? in a minimal API
                    // binds from route and query only. documentId has always worked this way.
                    [FromQuery] Guid? documentId,
                    [FromQuery] bool? confirmUpdates,
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

                    // 10 MB — matches the frontend UploadArea limit/label on the re-upload card,
                    // so a file that passes client validation is not rejected server-side.
                    const long maxFileSize = 10 * 1024 * 1024;
                    if (file.Length > maxFileSize)
                    {
                        return Results.Problem(
                            title: "File too large",
                            detail: "File size must not exceed 10 MB.",
                            statusCode: StatusCodes.Status400BadRequest);
                    }

                    using var stream = file.OpenReadStream();

                    var command = new UploadBlockReappraisalUnitsCommand(
                        appraisalId, file.FileName, documentId, stream, confirmUpdates ?? false);

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<UploadBlockReappraisalUnitsResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("UploadBlockReappraisalUnits")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<UploadBlockReappraisalUnitsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Re-match block reappraisal units from Excel")
            .WithDescription(
                "Non-destructive re-match: units absent from the Excel are auto-marked sold (MarkSoldByReappraisal). " +
                "Units present in the Excel are confirmed unsold. Attribute changes to existing units and rows the " +
                "Excel adds are applied only when the QUERY parameter confirmUpdates=true; without it the request " +
                "is rejected with 400 so the caller reviews the preview first. Same .xlsx column layout as the " +
                "standard upload endpoint.")
            .WithTags("Project")
            .RequireAuthorization()
            .DisableAntiforgery();
    }
}
