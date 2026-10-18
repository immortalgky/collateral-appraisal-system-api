using Appraisal.Application.Configurations;
using Appraisal.Application.Services;
using Shared.Time;

namespace Appraisal.Application.Features.Project.ImportProjectUnitPrices;

/// <summary>
/// Applies an Excel price list to the project's units. Stateless: the file is parsed again here, so
/// nothing a client previewed can be tampered with between preview and import.
/// </summary>
public record ImportUnitPricesCommand(Guid AppraisalId, string FileName, byte[] FileContent)
    : ICommand<UnitPriceImportPreviewResponse>, ITransactionalCommand<IAppraisalUnitOfWork>;

public class ImportUnitPricesCommandHandler(
    IProjectRepository projectRepository,
    AppraisalDbContext dbContext,
    IAppraisalUnitOfWork unitOfWork,
    AppraisalValuationSummaryService valuationSummaryService,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider
) : ICommandHandler<ImportUnitPricesCommand, UnitPriceImportPreviewResponse>
{
    private const int MaxErrorsInMessage = 5;

    public async Task<UnitPriceImportPreviewResponse> Handle(
        ImportUnitPricesCommand command, CancellationToken cancellationToken)
    {
        // Reject an unreadable file before touching the database.
        UnitPriceExcelParser.EnsureReadable(command.FileContent);

        dbContext.DetachAddedUnitPrices();

        var project = await projectRepository.GetByAppraisalIdAsync(command.AppraisalId, cancellationToken)
                      ?? throw new NotFoundException("Project", command.AppraisalId);

        var layout = UnitPriceImportLayout.For(project.ProjectType);
        var units = await LoadedUnitPrices.LoadUnitsAsync(dbContext, project.Id, cancellationToken);
        // Tracked: the import writes these rows through the DbContext, like CalculateProjectUnitPricesCommandHandler.
        var prices = await LoadedUnitPrices.PricesOf(dbContext, project.Id)
            .ToDictionaryAsync(p => p.ProjectUnitId, cancellationToken);

        using var stream = new MemoryStream(command.FileContent);
        var sheet = UnitPriceExcelParser.Parse(stream, layout);
        var analysis = UnitPriceImportAnalyzer.Analyze(
            command.FileName, sheet, layout, units,
            prices.ToDictionary(kv => kv.Key, kv => kv.Value.TotalAppraisalValueRounded));

        var response = analysis.Response;
        if (response.Errors.Count > 0 || response.MatchedCount == 0)
        {
            var first = response.Errors
                .Take(MaxErrorsInMessage)
                .Select(e => $"row {e.RowNumber}: {e.Message}");
            throw new BadRequestException(
                response.Errors.Count > 0
                    ? $"The price list has {response.Errors.Count} error(s)."
                    : "The price list matches no unit in the project.",
                string.Join("; ", first));
        }

        foreach (var (unitId, row) in analysis.Matched)
        {
            if (!prices.TryGetValue(unitId, out var price))
            {
                price = ProjectUnitPrice.Create(unitId);
                dbContext.ProjectUnitPrices.Add(price);
            }

            price.ApplyImportedValues(row.StandardPrice, row.AppraisalValue, row.ForceSellingPrice, row.CoverageAmount);
        }

        // A unit the file does not mention must not keep an old figure beside the imported ones.
        foreach (var unitId in analysis.MissingUnitIds)
        {
            if (prices.TryGetValue(unitId, out var price))
                price.ClearImportedValues();
        }

        project.MarkUnitPricesImported(command.FileName, dateTimeProvider.ApplicationNow, currentUser.UserCode);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Same as CalculateProjectUnitPrices: persist the block total to ValuationAnalyses (and publish the
        // appraised-value change for the approval tier) inside this transaction, after the prices are saved.
        await valuationSummaryService.RecomputeAsync(command.AppraisalId, cancellationToken);

        return response;
    }
}

public class ImportUnitPricesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/project/unit-prices/import",
                async (Guid appraisalId, IFormFile file, ISender sender, CancellationToken cancellationToken) =>
                {
                    if (ProjectExcelUpload.Validate(file) is { } problem) return problem;

                    var result = await sender.Send(
                        new ImportUnitPricesCommand(
                            appraisalId, ProjectExcelUpload.SafeName(file), await ProjectExcelUpload.ReadAsync(file, cancellationToken)),
                        cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("ImportUnitPrices")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<UnitPriceImportPreviewResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Import unit prices from Excel")
            .WithDescription(
                "Re-parses the price list and, when it has no blocking error, writes the file's prices to the " +
                "matched units, clears the prices of units the file does not mention, switches the project's " +
                "price source to Imported and recomputes the appraisal total, all in one transaction.")
            .WithTags("Project")
            .DisableAntiforgery();
    }
}
