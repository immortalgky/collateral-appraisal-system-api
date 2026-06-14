using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Time;

namespace Shared.Scheduling;

public static class RecurringJobScheduleExtensions
{
    /// <summary>
    /// Seeds + registers a module's recurring jobs from its own <c>{schema}.JobSchedules</c> table.
    /// Call inside <c>UseXModule</c> AFTER <c>app.UseMigration&lt;TContext&gt;()</c> so the table exists.
    ///
    /// For each definition: a missing DB row is seeded from the code default; then the job is registered
    /// with the DB schedule (cron/timezone/enabled), falling back to the code default cron when the row
    /// is absent or carries an invalid cron. Uses the DI-resolved <see cref="IRecurringJobManager"/>
    /// (not the static <c>RecurringJob</c> facade) so it works regardless of when <c>UseHangfire</c> runs.
    /// </summary>
    public static IApplicationBuilder UseModuleRecurringJobs<TContext>(
        this IApplicationBuilder app,
        IReadOnlyList<RecurringJobDefinition> jobs)
        where TContext : DbContext
    {
        using var scope = app.ApplicationServices.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<TContext>();
        var manager = sp.GetRequiredService<IRecurringJobManager>();
        var appTimeZone = sp.GetRequiredService<IDateTimeProvider>().ApplicationTimeZone;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Shared.Scheduling.RecurringJobs");

        var set = db.Set<JobSchedule>();

        // Seed-if-missing: the code definition list is the single source of truth.
        var existingIds = set.AsNoTracking().Select(s => s.JobId).ToHashSet();
        var seeded = false;
        foreach (var def in jobs)
        {
            if (existingIds.Contains(def.JobId)) continue;
            set.Add(JobSchedule.Create(def.JobId, def.DefaultCron, description: def.Description));
            logger.LogInformation("Seeding JobSchedule '{JobId}' into {Context}.", def.JobId, typeof(TContext).Name);
            seeded = true;
        }

        if (seeded)
        {
            try
            {
                db.SaveChanges();
            }
            catch (DbUpdateException ex)
            {
                // On N=2, both servers can seed an empty table concurrently; the second insert loses the
                // UX_JobSchedules_JobId race. The rows are now present either way, so swallow and continue.
                logger.LogInformation(ex,
                    "Concurrent seed of JobSchedules into {Context} hit a unique conflict; rows already exist.",
                    typeof(TContext).Name);
                // Drop the failed-Added entities so the context is clean for the re-read below.
                db.ChangeTracker.Clear();
            }
        }

        var rows = set.AsNoTracking().ToDictionary(s => s.JobId);

        foreach (var def in jobs)
        {
            rows.TryGetValue(def.JobId, out var row);

            if (row is { IsEnabled: false })
            {
                manager.RemoveIfExists(def.JobId);
                logger.LogInformation("Recurring job '{JobId}' is disabled in JobSchedules; removed.", def.JobId);
                continue;
            }

            var cron = row?.CronExpression ?? def.DefaultCron;

            var timeZone = appTimeZone;
            if (row?.TimeZoneId is { } tzId
                && !TimeZoneInfo.TryFindSystemTimeZoneById(tzId, out timeZone!))
            {
                logger.LogWarning(
                    "Recurring job '{JobId}' has unknown TimeZoneId '{TimeZoneId}'; using application default '{Default}'.",
                    def.JobId, tzId, appTimeZone.Id);
                timeZone = appTimeZone;
            }

            try
            {
                def.Register(manager, cron, new RecurringJobOptions { TimeZone = timeZone });
            }
            catch (ArgumentException ex)
            {
                // Hangfire validates the cron in AddOrUpdate and throws ArgumentException. An invalid cron
                // in the DB must not take down the app — log and fall back to the code default + app timezone.
                logger.LogError(ex,
                    "Recurring job '{JobId}' has invalid cron '{Cron}'; falling back to default '{Default}'.",
                    def.JobId, cron, def.DefaultCron);
                def.Register(manager, def.DefaultCron, new RecurringJobOptions { TimeZone = appTimeZone });
            }
        }

        // Surface rows in this module's table that match no code definition (likely a typo in JobId).
        var knownIds = jobs.Select(j => j.JobId).ToHashSet();
        foreach (var orphan in rows.Keys.Where(k => !knownIds.Contains(k)))
        {
            logger.LogWarning(
                "JobSchedules row '{JobId}' in {Context} matches no known job and was ignored.",
                orphan, typeof(TContext).Name);
        }

        return app;
    }
}
