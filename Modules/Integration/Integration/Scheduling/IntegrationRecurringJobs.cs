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

        new RecurringJobDefinition("regulatory-export", "0 2 1 * *",
            "Full monthly regulatory (Basel/RDT) collateral snapshot (1st at 02:00).",
            (mgr, cron, opt) => mgr.AddOrUpdate<RegulatoryExportJob>(
                "regulatory-export", j => j.ExecuteAsync(CancellationToken.None), cron, opt)),

        // Chain-based rebuild of the same report. Ships DISABLED (see the JobSchedules seed script):
        // it exists to produce a shadow file next to v1's so the two can be compared on the same data
        // before the switch. Runs an hour after v1 so both read the same state.
        new RecurringJobDefinition("regulatory-export-v2", "0 3 1 * *",
            "Regulatory snapshot v2 — built from the appraisal chain instead of CollateralMaster (1st at 03:00).",
            (mgr, cron, opt) => mgr.AddOrUpdate<RegulatoryExportV2Job>(
                "regulatory-export-v2", j => j.ExecuteAsync(CancellationToken.None), cron, opt))
    };
}
