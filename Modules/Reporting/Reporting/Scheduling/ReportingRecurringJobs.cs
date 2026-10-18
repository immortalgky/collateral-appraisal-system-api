using Hangfire;
using Reporting.Application.Services;
using Reporting.Data;
using Shared.Data.Outbox;
using Shared.Scheduling;

namespace Reporting.Scheduling;

/// <summary>
/// Recurring jobs owned by the Reporting module. Seeded into and read from <c>reporting.JobSchedules</c>
/// by <c>app.UseModuleRecurringJobs&lt;ReportingDbContext&gt;()</c>.
/// </summary>
public static class ReportingRecurringJobs
{
    public static readonly IReadOnlyList<RecurringJobDefinition> All = new[]
    {
        new RecurringJobDefinition("report-artifact-cleanup", "0 3 * * *",
            "Delete expired report job rows and their on-disk PDFs (daily at 03:00).",
            (mgr, cron, opt) => mgr.AddOrUpdate<ReportArtifactCleanupJob>(
                "report-artifact-cleanup", j => j.ExecuteAsync(CancellationToken.None), cron, opt)),
        // Reporting has an
        // outbox too but had no cleanup job — stuck rows were never reset and Processed/Failed rows
        // were never purged. Same schedule/shape as every other module's entry.
        new RecurringJobDefinition("outbox-cleanup-reporting", "0 2 * * *",
            "Purge processed/dead-letter outbox messages for the Reporting module (daily at 02:00).",
            (mgr, cron, opt) => mgr.AddOrUpdate<OutboxCleanupJob<ReportingDbContext>>(
                "outbox-cleanup-reporting", j => j.ExecuteAsync(CancellationToken.None), cron, opt))
    };
}
