namespace Appraisal.Application.Features.Project.ImportProjectUnitPrices;

/// <summary>
/// A project's units and stored prices for the read-only price-list handlers (preview, template):
/// AsNoTracking projections, no entities.
/// </summary>
internal sealed record LoadedUnitPrices(
    Guid ProjectId,
    ProjectType ProjectType,
    List<UnitSnapshot> Units,
    Dictionary<Guid, PriceSnapshot> Prices)
{
    public UnitPriceImportLayout Layout => UnitPriceImportLayout.For(ProjectType);

    /// <summary>Stored appraisal value per unit (the preview's "previous").</summary>
    public Dictionary<Guid, decimal?> CurrentAppraisalValues() =>
        Prices.ToDictionary(kv => kv.Key, kv => kv.Value.AppraisalValue);

    /// <summary>Throws NotFound when the appraisal has no project.</summary>
    public static async Task<LoadedUnitPrices> LoadAsync(AppraisalDbContext db, Guid appraisalId, CancellationToken ct)
    {
        var project = await db.Projects
            .AsNoTracking()
            .Where(p => p.AppraisalId == appraisalId)
            .Select(p => new { p.Id, p.ProjectType })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Project", appraisalId);

        var units = await LoadUnitsAsync(db, project.Id, ct);

        var prices = await PricesOf(db, project.Id)
            .AsNoTracking()
            .Select(PriceSnapshot.Projection)
            .ToDictionaryAsync(x => x.ProjectUnitId, ct);

        return new LoadedUnitPrices(project.Id, project.ProjectType, units, prices);
    }

    public static Task<List<UnitSnapshot>> LoadUnitsAsync(AppraisalDbContext db, Guid projectId, CancellationToken ct) =>
        db.ProjectUnits
            .AsNoTracking()
            .Where(u => u.ProjectId == projectId)
            .Select(UnitSnapshot.Projection)
            .ToListAsync(ct);

    /// <summary>The price rows of a project's units (tracked unless the caller says otherwise).</summary>
    public static IQueryable<ProjectUnitPrice> PricesOf(AppraisalDbContext db, Guid projectId) =>
        db.ProjectUnitPrices.Where(p => db.ProjectUnits.Any(u => u.Id == p.ProjectUnitId && u.ProjectId == projectId));
}
