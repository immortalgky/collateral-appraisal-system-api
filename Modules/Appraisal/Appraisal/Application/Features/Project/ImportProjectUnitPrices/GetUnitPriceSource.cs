namespace Appraisal.Application.Features.Project.ImportProjectUnitPrices;

public record GetUnitPriceSourceQuery(Guid AppraisalId) : IQuery<UnitPriceSourceDto>;

/// <summary>Where the project's unit prices come from, plus the last import's details.</summary>
/// <param name="PricedUnitCount">Units that currently have an appraisal value.</param>
public record UnitPriceSourceDto(
    string Source, string? FileName, DateTime? ImportedAt, string? ImportedBy, int PricedUnitCount);

public class GetUnitPriceSourceQueryHandler(AppraisalDbContext dbContext)
    : IQueryHandler<GetUnitPriceSourceQuery, UnitPriceSourceDto>
{
    public async Task<UnitPriceSourceDto> Handle(GetUnitPriceSourceQuery query, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .AsNoTracking()
            .Where(p => p.AppraisalId == query.AppraisalId)
            .Select(p => new { p.Id, p.UnitPriceSource, p.UnitPriceImportFileName, p.UnitPriceImportedAt, p.UnitPriceImportedBy })
            .FirstOrDefaultAsync(cancellationToken);

        // No project yet = nothing imported; same leniency as GetProjectUnitPrices.
        if (project is null)
            return new UnitPriceSourceDto(nameof(UnitPriceSource.Calculated), null, null, null, 0);

        var priced = await dbContext.ProjectUnitPrices
            .AsNoTracking()
            .CountAsync(p => p.TotalAppraisalValueRounded != null
                             && dbContext.ProjectUnits.Any(u => u.Id == p.ProjectUnitId && u.ProjectId == project.Id),
                cancellationToken);

        return new UnitPriceSourceDto(
            project.UnitPriceSource.ToString(),
            project.UnitPriceImportFileName,
            project.UnitPriceImportedAt,
            project.UnitPriceImportedBy,
            priced);
    }
}

public class GetUnitPriceSourceEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/project/unit-prices/source",
                async (Guid appraisalId, ISender sender, CancellationToken cancellationToken) =>
                    Results.Ok(await sender.Send(new GetUnitPriceSourceQuery(appraisalId), cancellationToken)))
            .WithName("GetUnitPriceSource")
            .Produces<UnitPriceSourceDto>()
            .WithSummary("Get the project's unit-price source")
            .WithDescription("Calculated or Imported, with the file name, time and user of the last import.")
            .WithTags("Project");
    }
}
