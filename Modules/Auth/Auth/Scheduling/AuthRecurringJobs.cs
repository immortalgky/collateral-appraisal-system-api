using Hangfire;
using Shared.Scheduling;

namespace Auth.Scheduling;

/// <summary>
/// Recurring jobs owned by the Auth module. Seeded into and read from <c>auth.JobSchedules</c>
/// by <c>app.UseModuleRecurringJobs&lt;AuthDbContext&gt;()</c>.
/// </summary>
public static class AuthRecurringJobs
{
    public static readonly IReadOnlyList<RecurringJobDefinition> All = new[]
    {
        new RecurringJobDefinition("user-session-cleanup", "0 3 * * *",
            "Delete browser-session rows 90 days after they ended (daily at 03:00).",
            (mgr, cron, opt) => mgr.AddOrUpdate<UserSessionCleanupJob>(
                "user-session-cleanup", j => j.ExecuteAsync(CancellationToken.None), cron, opt))
    };
}
