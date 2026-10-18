namespace Appraisal.Application.Features.Project;

internal static class ProjectUnitPriceTracking
{
    /// <summary>
    /// Drops ProjectUnitPrice rows an earlier attempt of the same command left in the change tracker as Added.
    /// A handler that runs again after an EF execution-strategy retry would otherwise add them twice.
    /// </summary>
    public static void DetachAddedUnitPrices(this AppraisalDbContext db)
    {
        foreach (var entry in db.ChangeTracker.Entries<ProjectUnitPrice>().Where(e => e.State == EntityState.Added).ToList())
            entry.State = EntityState.Detached;
    }
}
