using Hangfire;

namespace Shared.Scheduling;

/// <summary>
/// Code-side definition of a recurring job owned by a module: its id, default schedule, and the typed
/// Hangfire registration. The <see cref="Register"/> delegate is the only place the concrete job class
/// appears — it must live in the owning module, which references that job type. Example:
/// <code>
/// new RecurringJobDefinition("outbox-cleanup-request", "0 2 * * *", "Purge ...",
///     (mgr, cron, opt) => mgr.AddOrUpdate&lt;OutboxCleanupJob&lt;RequestDbContext&gt;&gt;(
///         "outbox-cleanup-request", j => j.ExecuteAsync(CancellationToken.None), cron, opt));
/// </code>
/// </summary>
/// <param name="JobId">Hangfire recurring job id; also the <see cref="JobSchedule.JobId"/> key.</param>
/// <param name="DefaultCron">Fallback cron used when no DB row exists or the DB cron is invalid.</param>
/// <param name="Description">Human-readable purpose, seeded into the DB row for ops.</param>
/// <param name="Register">Binds the id to its concrete job class via the supplied manager/cron/options.</param>
public sealed record RecurringJobDefinition(
    string JobId,
    string DefaultCron,
    string Description,
    Action<IRecurringJobManager, string, RecurringJobOptions> Register);
