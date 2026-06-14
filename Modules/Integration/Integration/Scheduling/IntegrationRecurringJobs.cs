using Hangfire;
using Integration.FileInterface.Jobs.CollateralResult;
using Integration.FileInterface.Jobs.Reappraisal;
using Integration.FileInterface.Jobs.RegulatoryExport;
using Shared.Scheduling;

namespace Integration.Scheduling;

/// <summary>
/// Recurring jobs owned by the Integration module. Seeded into and read from
/// <c>integration.JobSchedules</c> by <c>app.UseModuleRecurringJobs&lt;IntegrationDbContext&gt;()</c>.
/// </summary>
public static class IntegrationRecurringJobs
{
    public static readonly IReadOnlyList<RecurringJobDefinition> All = new[]
    {
        new RecurringJobDefinition("reappraisal-as400", "0 1 1 * *",
            "Ingest AS400 COLLATREV reappraisal files (monthly, 1st at 01:00).",
            (mgr, cron, opt) => mgr.AddOrUpdate<As400ReappraisalJob>(
                "reappraisal-as400", j => j.ExecuteAsync(CancellationToken.None), cron, opt)),

        new RecurringJobDefinition("collateral-result-export", "0 0 * * *",
            "Ship completed-appraisal prices to the AS400 host (daily at midnight).",
            (mgr, cron, opt) => mgr.AddOrUpdate<CollateralResultExportJob>(
                "collateral-result-export", j => j.ExecuteAsync(CancellationToken.None), cron, opt)),

        new RecurringJobDefinition("regulatory-export", "0 2 1 * *",
            "Full monthly regulatory (Basel/RDT) collateral snapshot (1st at 02:00).",
            (mgr, cron, opt) => mgr.AddOrUpdate<RegulatoryExportJob>(
                "regulatory-export", j => j.ExecuteAsync(CancellationToken.None), cron, opt))
    };
}
