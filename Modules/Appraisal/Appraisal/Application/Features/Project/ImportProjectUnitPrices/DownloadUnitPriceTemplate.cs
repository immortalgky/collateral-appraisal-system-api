namespace Appraisal.Application.Features.Project.ImportProjectUnitPrices;

public record DownloadUnitPriceTemplateQuery(Guid AppraisalId) : IQuery<UnitPriceTemplateFile>;

public record UnitPriceTemplateFile(byte[] Bytes, string FileName);

public class DownloadUnitPriceTemplateQueryHandler(AppraisalDbContext dbContext)
    : IQueryHandler<DownloadUnitPriceTemplateQuery, UnitPriceTemplateFile>
{
    public async Task<UnitPriceTemplateFile> Handle(
        DownloadUnitPriceTemplateQuery query, CancellationToken cancellationToken)
    {
        var data = await LoadedUnitPrices.LoadAsync(dbContext, query.AppraisalId, cancellationToken);

        var appraisalNumber = await dbContext.Appraisals
            .AsNoTracking()
            .Where(a => a.Id == query.AppraisalId)
            .Select(a => a.AppraisalNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var bytes = UnitPriceTemplateBuilder.Build(data.Layout, data.Units, data.Prices);

        // The appraisal number is user-shaped text: keep it out of the header unless it is a safe file name.
        var label = string.IsNullOrWhiteSpace(appraisalNumber)
            ? query.AppraisalId.ToString()
            : string.Concat(appraisalNumber.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c));

        return new UnitPriceTemplateFile(bytes, $"unit-prices-{label}.xlsx");
    }
}

public class DownloadUnitPriceTemplateEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/project/unit-prices/template",
                async (Guid appraisalId, ISender sender, CancellationToken cancellationToken) =>
                {
                    var file = await sender.Send(new DownloadUnitPriceTemplateQuery(appraisalId), cancellationToken);
                    return Results.File(file.Bytes, UnitPriceTemplateBuilder.ContentType, file.FileName);
                })
            .WithName("DownloadUnitPriceTemplate")
            .Produces<IResult>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Download the unit price-list Excel")
            .WithDescription(
                "One row per project unit (sold included) with the stored prices, headers per project type. " +
                "Edit and upload it to the import endpoints.")
            .WithTags("Project");
    }
}
