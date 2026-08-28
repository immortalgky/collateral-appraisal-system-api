namespace Request.Application.Features.RequestTitles.ImportTitles;

/// <summary>
/// Bulk entry for the request's title list.
///
/// Neither route takes a request id, and neither writes anything: they parse, validate and hand the
/// rows back. The browser shows them for confirmation and drops the accepted ones into the form,
/// which is then saved by the ordinary create/update request call. That is what makes this usable
/// while a brand-new request is still being typed and has no id to attach rows to.
/// </summary>
public class TitleImportEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/requests/titles/import-template",
                async (ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new TitleImportTemplateQuery(), cancellationToken);
                    return Results.File(result.FileBytes, result.ContentType, result.FileName);
                })
            .WithName("DownloadTitleImportTemplate")
            .Produces<IResult>(StatusCodes.Status200OK)
            .WithTags("RequestTitles")
            .WithSummary("Download the title bulk-import Excel template")
            .WithDescription(
                "Returns an .xlsx with one sheet per collateral family, a Reference sheet listing the " +
                "parameter codes that are valid right now, and an Instructions sheet.")
            .RequireAuthorization();

        app.MapPost("/requests/titles/import-preview",
                async (IFormFile file, ISender sender, CancellationToken cancellationToken) =>
                {
                    var problem = ValidateFile(file);
                    if (problem is not null) return problem;

                    await using var stream = file.OpenReadStream();
                    var result = await sender.Send(new ParseTitleImportFileQuery(stream), cancellationToken);
                    return Results.Ok(TitleImportPreviewResponse.From(result));
                })
            .WithName("PreviewTitleImportFile")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<TitleImportPreviewResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("RequestTitles")
            .WithSummary("Parse an uploaded title Excel file and return a preview")
            .WithDescription(
                "Nothing is saved. Rows that pass every check come back in 'rows'; the rest are described " +
                "row by row in 'errors' so the user can fix the file. A file with some bad rows still " +
                "returns 200 — partial import is the point. 400 means the file itself is unusable.")
            .DisableAntiforgery()
            .RequireAuthorization();

        app.MapPost("/requests/titles/import-preview/paste",
                async (PasteTitleImportRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    if (string.IsNullOrWhiteSpace(request.Tsv))
                        return Problem("No data",
                            "Copy the range from Excel, including the header row, and paste it again.");

                    if (request.Tsv.Length > TitleImportLimits.MaxPasteChars)
                        return Problem("Pasted data too large",
                            $"At most {TitleImportLimits.MaxPasteChars / 1024} KB can be pasted at once.");

                    var result = await sender.Send(
                        new ParseTitleImportPasteQuery(request.Sheet, request.Tsv), cancellationToken);

                    return Results.Ok(TitleImportPreviewResponse.From(result));
                })
            .WithName("PreviewTitleImportPaste")
            .Produces<TitleImportPreviewResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("RequestTitles")
            .WithSummary("Parse cells pasted from Excel and return a preview")
            .WithDescription(
                "Same validation and same response as the file upload — only the reader differs. " +
                "'sheet' names the collateral family the pasted columns belong to (Land, Condo, …), " +
                "because each family has its own column set.")
            .RequireAuthorization();
    }

    private static IResult? ValidateFile(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName);
        if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
            return Problem("Invalid file type", "Only .xlsx files are supported.");

        if (file.Length > TitleImportLimits.MaxFileBytes)
            return Problem("File too large",
                $"File size must not exceed {TitleImportLimits.MaxFileBytes / (1024 * 1024)} MB.");

        return null;
    }

    private static IResult Problem(string title, string detail)
        => Results.Problem(title: title, detail: detail, statusCode: StatusCodes.Status400BadRequest);
}

public record PasteTitleImportRequest(string Sheet, string Tsv);

/// <param name="IgnoredSheets">Worksheets the file carried that the importer does not recognise — surfaced so a user who renamed a sheet finds out why nothing came through.</param>
/// <param name="MissingColumns">Columns a row needed that the sheet does not have — see TitleImportResult.</param>
public record TitleImportPreviewResponse(
    int TotalRows,
    int ValidRows,
    int InvalidRows,
    IReadOnlyList<TitleImportRow> Rows,
    IReadOnlyList<TitleImportRowError> Errors,
    IReadOnlyList<string> IgnoredSheets,
    IReadOnlyList<string> MissingColumns)
{
    public static TitleImportPreviewResponse From(TitleImportResult result)
        => new(result.TotalRows, result.ValidRows, result.InvalidRows,
            result.Rows, result.Errors, result.IgnoredSheets, result.MissingColumns);
}
