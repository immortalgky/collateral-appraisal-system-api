using Hangfire;
using Integration.FailedMessages;
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
        // THE THREE MONTHLY AS400 JOBS RUN ON THE 2ND OF THE MONTH, in the order they depend on each other:
        // the two ingests at 02:00, then the regulatory snapshot at 03:00. The 2nd rather than the 1st because
        // AS400 produces the files after month-end close. The collateral-result export is the exception: it
        // runs daily at 04:00, below.
        //
        // The one-hour gaps are the whole schedule. Anything that makes an ingest take longer than an
        // hour — a backlog of files, a slow host — lets the regulatory export read last month's data, and it
        // will not fail while doing it. Move the export later rather than the ingests earlier.
        //
        // These are starting points; an admin retunes them in integration.JobSchedules through
        // /admin/job-schedules without a deploy, and the DB row wins. Changing a value here only affects a
        // database that has never seeded the row (see UseModuleRecurringJobs: seed-if-missing).
        new RecurringJobDefinition("reappraisal-as400", "0 2 2 * *",
            "Ingest AS400 COLLATREV reappraisal files (monthly, 2nd at 02:00).",
            (mgr, cron, opt) => mgr.AddOrUpdate<As400ReappraisalJob>(
                "reappraisal-as400", j => j.ExecuteAsync(CancellationToken.None), cron, opt)),

        // MUST stay ahead of regulatory-export, which takes its whole row set from collateral.HostCollateralLinks:
        // running it first ships a month-old picture. The result export no longer needs a host link to send a row
        // (a manual request's collateral id comes from walking the links when one exists, and goes out blank
        // otherwise), but a fresher link file means fewer blank ids.
        new RecurringJobDefinition("host-collateral-link-as400", "0 2 2 * *",
            "Ingest AS400 COLLATLINK host-collateral-id files (monthly, 2nd at 02:00, ahead of the regulatory export).",
            (mgr, cron, opt) => mgr.AddOrUpdate<As400HostLinkJob>(
                "host-collateral-link-as400", j => j.ExecuteAsync(CancellationToken.None), cron, opt)),

        // Daily at 04:00 (the schedule is owned by the admin UI; this value only seeds a fresh database): it sends
        // each newly completed, in-scope reappraisal once, and it is the only AS400 output the host reads back into
        // its own month-end run.
        new RecurringJobDefinition("collateral-result-export", "0 4 * * *",
            "Ship completed-appraisal prices to the AS400 host (daily at 04:00).",
            (mgr, cron, opt) => mgr.AddOrUpdate<CollateralResultExportJob>(
                "collateral-result-export", j => j.ExecuteAsync(CancellationToken.None), cron, opt)),

        // One row per collateral AS400 reports, carrying that collateral's FIRST appraisal.
        new RecurringJobDefinition("regulatory-export", "0 3 2 * *",
            "Full monthly regulatory (Basel/RDT) collateral snapshot (2nd at 03:00, after the COLLATLINK ingest).",
            (mgr, cron, opt) => mgr.AddOrUpdate<RegulatoryExportJob>(
                "regulatory-export", j => j.ExecuteAsync(CancellationToken.None), cron, opt)),

        // Retention for the Failed Messages screen (design D6). 02:30 so it doesn't collide with the
        // per-module outbox-/inbox-cleanup-* jobs (02:00) or Reporting's report-artifact-cleanup (03:00).
        new RecurringJobDefinition("failed-messages-cleanup", "30 2 * * *",
            "Purge Retried/Discarded FailedMessages rows older than 90 days, Pending Skipped rows idle longer than " +
            "FailedMessages:SkippedPendingRetentionDays (default 30), and broker snapshots not refreshed for 7 days (daily at 02:30).",
            (mgr, cron, opt) => mgr.AddOrUpdate<FailedMessageCleanupJob>(
                "failed-messages-cleanup", j => j.ExecuteAsync(CancellationToken.None), cron, opt))
    };
}
