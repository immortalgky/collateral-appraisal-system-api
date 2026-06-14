using Hangfire;
using Request.Infrastructure;
using Shared.Data.Outbox;
using Shared.Scheduling;

namespace Request.Scheduling;

/// <summary>
/// Recurring jobs owned by the Request module. Seeded into and read from <c>request.JobSchedules</c>
/// by <c>app.UseModuleRecurringJobs&lt;RequestDbContext&gt;()</c>.
/// </summary>
public static class RequestRecurringJobs
{
    public static readonly IReadOnlyList<RecurringJobDefinition> All = new[]
    {
        new RecurringJobDefinition("outbox-cleanup-request", "0 2 * * *",
            "Purge processed/dead-letter outbox messages for the Request module (daily at 02:00).",
            (mgr, cron, opt) => mgr.AddOrUpdate<OutboxCleanupJob<RequestDbContext>>(
                "outbox-cleanup-request", j => j.ExecuteAsync(CancellationToken.None), cron, opt))
    };
}
