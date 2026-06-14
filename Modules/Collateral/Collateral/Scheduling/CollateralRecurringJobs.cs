using Collateral.CollateralMasters.Services;
using Collateral.Data;
using Hangfire;
using Shared.Data.Outbox;
using Shared.Scheduling;

namespace Collateral.Scheduling;

/// <summary>
/// Recurring jobs owned by the Collateral module. Seeded into and read from <c>collateral.JobSchedules</c>
/// by <c>app.UseModuleRecurringJobs&lt;CollateralDbContext&gt;()</c>.
/// </summary>
public static class CollateralRecurringJobs
{
    public static readonly IReadOnlyList<RecurringJobDefinition> All = new[]
    {
        new RecurringJobDefinition("outbox-cleanup-collateral", "0 2 * * *",
            "Purge processed/dead-letter outbox messages for the Collateral module (daily at 02:00).",
            (mgr, cron, opt) => mgr.AddOrUpdate<OutboxCleanupJob<CollateralDbContext>>(
                "outbox-cleanup-collateral", j => j.ExecuteAsync(CancellationToken.None), cron, opt)),

        new RecurringJobDefinition("reappraisal-block", "0 1 * * *",
            "Scan block projects past their reappraisal interval (daily at 01:00).",
            (mgr, cron, opt) => mgr.AddOrUpdate<BlockReappraisalJob>(
                "reappraisal-block", j => j.ExecuteAsync(CancellationToken.None), cron, opt))
    };
}
