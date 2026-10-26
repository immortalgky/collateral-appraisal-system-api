namespace Appraisal.Application.Features.Project.ImportProjectUnitPrices;

/// <summary>Parses and matches a price list against the project's units. Writes nothing.</summary>
public record PreviewUnitPriceImportQuery(Guid AppraisalId, string FileName, byte[] FileContent)
    : IQuery<UnitPriceImportPreviewResponse>;

public class PreviewUnitPriceImportQueryHandler(AppraisalDbContext dbContext)
    : IQueryHandler<PreviewUnitPriceImportQuery, UnitPriceImportPreviewResponse>
{
    public async Task<UnitPriceImportPreviewResponse> Handle(
        PreviewUnitPriceImportQuery query, CancellationToken cancellationToken)
    {
        // Reject an unreadable file before touching the database.
        UnitPriceExcelParser.EnsureReadable(query.FileContent);

        var data = await LoadedUnitPrices.LoadAsync(dbContext, query.AppraisalId, cancellationToken);

        using var stream = new MemoryStream(query.FileContent);
        var sheet = UnitPriceExcelParser.Parse(stream, data.Layout);
        return UnitPriceImportAnalyzer
            .Analyze(query.FileName, sheet, data.Layout, data.Units, data.CurrentAppraisalValues())
            .Response;
    }
}

public class PreviewUnitPriceImportEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/project/unit-prices/import/preview",
                async (Guid appraisalId, IFormFile file, ISender sender, CancellationToken cancellationToken) =>
                {
                    if (ProjectExcelUpload.Validate(file) is { } problem) return problem;

                    var result = await sender.Send(
                        new PreviewUnitPriceImportQuery(
                            appraisalId, ProjectExcelUpload.SafeName(file), await ProjectExcelUpload.ReadAsync(file, cancellationToken)),
                        cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("PreviewUnitPriceImport")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<UnitPriceImportPreviewResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Preview a unit price-list import")
            .WithDescription(
                "Parses an Excel price list and matches it to the project's units. Writes nothing; " +
                "returns blocking errors, compare-only differences and the sheet-total check.")
            .WithTags("Project")
            .DisableAntiforgery();
    }
}
