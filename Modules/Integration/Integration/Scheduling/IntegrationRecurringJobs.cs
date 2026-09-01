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
        // ALL FOUR AS400 JOBS RUN ON THE 2ND OF THE MONTH, in the order they depend on each other:
        // the two ingests at 02:00, the regulatory snapshot at 03:00, the result export at 04:00.
        // The 2nd rather than the 1st because AS400 produces the files after month-end close.
        //
        // The one-hour gaps are the whole schedule. Anything that makes an ingest take longer than an
        // hour — a backlog of files, a slow host — lets the exports read last month's data, and they
        // will not fail while doing it. Move the exports later rather than the ingests earlier.
        //
        // These are starting points; an admin retunes them in integration.JobSchedules through
        // /admin/job-schedules without a deploy, and the DB row wins. Changing a value here only affects a
        // database that has never seeded the row (see UseModuleRecurringJobs: seed-if-missing).
        new RecurringJobDefinition("reappraisal-as400", "0 2 2 * *",
            "Ingest AS400 COLLATREV reappraisal files (monthly, 2nd at 02:00).",
            (mgr, cron, opt) => mgr.AddOrUpdate<As400ReappraisalJob>(
                "reappraisal-as400", j => j.ExecuteAsync(CancellationToken.None), cron, opt)),

        // MUST stay ahead of BOTH exports below: regulatory-export takes its whole row set from
        // collateral.HostCollateralLinks, and collateral-result-export can only emit an appraisal
        // that already has a host link. Running either first ships a month-old picture.
        new RecurringJobDefinition("host-collateral-link-as400", "0 2 2 * *",
            "Ingest AS400 COLLATLINK host-collateral-id files (monthly, 2nd at 02:00, ahead of both exports).",
            (mgr, cron, opt) => mgr.AddOrUpdate<As400HostLinkJob>(
                "host-collateral-link-as400", j => j.ExecuteAsync(CancellationToken.None), cron, opt)),

        // Last of the four: it echoes the ids the link ingest lands, and it is the only one whose
        // output the host reads back into its own month-end run.
        new RecurringJobDefinition("collateral-result-export", "0 4 2 * *",
            "Ship completed-appraisal prices to the AS400 host (monthly, 2nd at 04:00).",
            (mgr, cron, opt) => mgr.AddOrUpdate<CollateralResultExportJob>(
                "collateral-result-export", j => j.ExecuteAsync(CancellationToken.None), cron, opt)),

        // One row per collateral AS400 reports, carrying that collateral's FIRST appraisal.
        new RecurringJobDefinition("regulatory-export", "0 3 2 * *",
            "Full monthly regulatory (Basel/RDT) collateral snapshot (2nd at 03:00, after the COLLATLINK ingest).",
            (mgr, cron, opt) => mgr.AddOrUpdate<RegulatoryExportJob>(
                "regulatory-export", j => j.ExecuteAsync(CancellationToken.None), cron, opt))
    };
}
