using Hangfire;
using Shared.Data.Outbox;
using Shared.Scheduling;
using Workflow.Data;

namespace Workflow.Scheduling;

/// <summary>
/// Recurring jobs owned by the Workflow module. Seeded into and read from <c>workflow.JobSchedules</c>
/// by <c>app.UseModuleRecurringJobs&lt;WorkflowDbContext&gt;()</c>.
/// </summary>
public static class WorkflowRecurringJobs
{
    public static readonly IReadOnlyList<RecurringJobDefinition> All = new[]
    {
        new RecurringJobDefinition("outbox-cleanup-workflow", "0 2 * * *",
            "Purge processed/dead-letter outbox messages for the Workflow module (daily at 02:00).",
            (mgr, cron, opt) => mgr.AddOrUpdate<OutboxCleanupJob<WorkflowDbContext>>(
                "outbox-cleanup-workflow", j => j.ExecuteAsync(CancellationToken.None), cron, opt))
    };
}
