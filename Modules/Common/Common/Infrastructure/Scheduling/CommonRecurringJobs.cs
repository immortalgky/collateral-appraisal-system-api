using Hangfire;
using Shared.Logging;
using Shared.Scheduling;

namespace Common.Infrastructure.Scheduling;

/// <summary>
/// Recurring jobs owned by the Common module. Single source of truth for their id/default-cron/
/// description; seeded into and read from <c>common.JobSchedules</c> by
/// <c>app.UseModuleRecurringJobs&lt;CommonDbContext&gt;()</c>.
/// </summary>
public static class CommonRecurringJobs
{
    public static readonly IReadOnlyList<RecurringJobDefinition> All = new[]
    {
        new RecurringJobDefinition("logs-cleanup", "0 3 * * *",
            "Purge dbo.Logs rows older than the retention window (daily at 03:00).",
            (mgr, cron, opt) => mgr.AddOrUpdate<LogsCleanupJob>(
                "logs-cleanup", j => j.ExecuteAsync(CancellationToken.None), cron, opt))
    };
}
