using Hangfire;
using Integration.FileInterface.Jobs.CollateralResult;
using Integration.FileInterface.Jobs.HostLink;
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

        // MUST stay ahead of collateral-result-export (00:00): the export echoes the ids this job
        // lands, so running it after would ship every id a day late.
        new RecurringJobDefinition("host-collateral-link-as400", "0 22 * * *",
            "Ingest AS400 COLLATLINK host-collateral-id files (nightly at 22:00, before the result export).",
            (mgr, cron, opt) => mgr.AddOrUpdate<As400HostLinkJob>(
                "host-collateral-link-as400", j => j.ExecuteAsync(CancellationToken.None), cron, opt)),

        new RecurringJobDefinition("collateral-result-export", "0 0 * * *",
            "Ship completed-appraisal prices to the AS400 host (daily at midnight).",
            (mgr, cron, opt) => mgr.AddOrUpdate<CollateralResultExportJob>(
                "collateral-result-export", j => j.ExecuteAsync(CancellationToken.None), cron, opt)),

        // One row per collateral AS400 reports, carrying that collateral's FIRST appraisal.
        //
        // MUST stay behind host-collateral-link-as400 (22:00 nightly): the row set comes from
        // collateral.HostCollateralLinks, so a stale feed means a stale file. 02:00 on the 1st is
        // several hours after the previous night's ingest, so the default cron already satisfies this
        // — but any change to either cron has to preserve the ordering.
        new RecurringJobDefinition("regulatory-export", "0 2 1 * *",
            "Full monthly regulatory (Basel/RDT) collateral snapshot (1st at 02:00).",
            (mgr, cron, opt) => mgr.AddOrUpdate<RegulatoryExportJob>(
                "regulatory-export", j => j.ExecuteAsync(CancellationToken.None), cron, opt))
    };
}
