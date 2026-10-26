namespace Appraisal.Application.Features.Project.ImportProjectUnitPrices;

public record GetUnitPriceSourceQuery(Guid AppraisalId) : IQuery<UnitPriceSourceDto>;

/// <summary>Where the project's unit prices come from, plus the last import's details.</summary>
/// <param name="PricedUnitCount">Units that currently have an appraisal value.</param>
/// <param name="TotalAppraisalValueRounded">Sum over every unit of the project, sold or not (as the Decision Summary does).</param>
/// <param name="TotalForceSellingPrice">Sum of the units' forced-sale price, over the same units.</param>
public record UnitPriceSourceDto(
    string Source, string? FileName, DateTime? ImportedAt, string? ImportedBy, int PricedUnitCount,
    decimal? TotalAppraisalValueRounded, decimal? TotalForceSellingPrice);

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
            return new UnitPriceSourceDto(nameof(UnitPriceSource.Calculated), null, null, null, 0, null, null);

        // One aggregate over the project's unit prices. Every unit counts, sold or not, and a unit with
        // no model still does (it is reached through ProjectUnits.ProjectId), like the Decision Summary's
        // block totals. EF turns Sum into COALESCE(SUM(..), 0), so nothing priced (Priced = 0) is mapped to null below.
        var totals = await dbContext.ProjectUnitPrices
            .AsNoTracking()
            .Where(p => dbContext.ProjectUnits.Any(u => u.Id == p.ProjectUnitId && u.ProjectId == project.Id))
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Priced = g.Count(p => p.TotalAppraisalValueRounded != null),
                Value = g.Sum(p => p.TotalAppraisalValueRounded),
                Force = g.Where(p => p.TotalAppraisalValueRounded != null).Sum(p => p.ForceSellingPrice),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new UnitPriceSourceDto(
            project.UnitPriceSource.ToString(),
            project.UnitPriceImportFileName,
            project.UnitPriceImportedAt,
            project.UnitPriceImportedBy,
            totals?.Priced ?? 0,
            totals is { Priced: > 0 } ? totals.Value : null,
            totals is { Priced: > 0 } ? totals.Force : null);
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
