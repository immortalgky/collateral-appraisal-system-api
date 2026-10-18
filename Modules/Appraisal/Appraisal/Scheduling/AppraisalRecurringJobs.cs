using Appraisal.Application.Services;
using Appraisal.Infrastructure;
using Hangfire;
using Shared.Data.Outbox;
using Shared.Scheduling;

namespace Appraisal.Scheduling;

/// <summary>
/// Recurring jobs owned by the Appraisal module. Seeded into and read from <c>appraisal.JobSchedules</c>
/// by <c>app.UseModuleRecurringJobs&lt;AppraisalDbContext&gt;()</c>.
/// </summary>
public static class AppraisalRecurringJobs
{
    public static readonly IReadOnlyList<RecurringJobDefinition> All = new[]
    {
        new RecurringJobDefinition("outbox-cleanup-appraisal", "0 2 * * *",
            "Purge processed/dead-letter outbox messages for the Appraisal module (daily at 02:00).",
            (mgr, cron, opt) => mgr.AddOrUpdate<OutboxCleanupJob<AppraisalDbContext>>(
                "outbox-cleanup-appraisal", j => j.ExecuteAsync(CancellationToken.None), cron, opt)),

        new RecurringJobDefinition("appraisal-sla-status-refresh", "0 * * * *",
            "Refresh appraisal.Appraisals.SLAStatus (OnTrack/AtRisk/Breached) for open appraisals (hourly).",
            (mgr, cron, opt) => mgr.AddOrUpdate<AppraisalSlaStatusRefreshJob>(
                "appraisal-sla-status-refresh", j => j.ExecuteAsync(CancellationToken.None), cron, opt))
    };
}
