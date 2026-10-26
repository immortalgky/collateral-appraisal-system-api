using Carter;
using Hangfire;
using Hangfire.States;
using Hangfire.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Scheduling;
using Shared.Time;

namespace Common.Application.Features.JobSchedules;

/// <summary>
/// Admin maintenance for the per-module <c>{schema}.JobSchedules</c> tables — the rows that override
/// a Hangfire recurring job's cron, timezone and enabled state.
///
/// Rows live in ten module schemas, so this reads and writes through
/// <see cref="IJobScheduleRegistry"/> rather than a DbContext of its own. The registry is populated
/// at startup by <c>UseModuleRecurringJobs&lt;TContext&gt;</c>, so a job absent from the code catalog
/// is not listed here — matching the startup behaviour, which ignores orphan rows.
///
/// Saving re-registers the job with Hangfire immediately: no restart is required.
///
/// Changing a cron affects every module's background processing, so this is gated on its own
/// JOB_SCHEDULE_MANAGE permission rather than being login-only.
/// </summary>
public class JobScheduleEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/job-schedules")
            .WithTags("JobSchedules")
            .RequireAuthorization("job-schedule.manage");

        group.MapGet("/", List)
            .WithName("GetJobSchedules")
            .Produces<List<JobScheduleDto>>()
            .WithSummary("List every administrable recurring job across all modules");

        group.MapPut("/{jobId}", Update)
            .WithName("UpdateJobSchedule")
            .Produces<JobScheduleDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update a recurring job's cron, timezone, or enabled state");
    }

    internal static async Task<IResult> List(
        IJobScheduleRegistry registry,
        IServiceProvider services,
        JobStorage storage,
        IDateTimeProvider clock,
        ILogger<JobScheduleEndpoints> logger,
        CancellationToken ct)
    {
        var registrations = registry.All;
        var runs = TryLoadRuns(storage, registrations.Select(r => r.JobId), clock, logger);
        var result = new List<JobScheduleDto>();

        foreach (var registration in registrations)
        {
            var row = await registration.LoadAsync(services, ct);
            result.Add(ToDto(registration, row, clock, runs));
        }

        return Results.Ok(result);
    }

    internal static async Task<IResult> Update(
        string jobId,
        UpdateJobScheduleRequest request,
        IJobScheduleRegistry registry,
        IRecurringJobManager manager,
        IServiceProvider services,
        JobStorage storage,
        IDateTimeProvider clock,
        ILogger<JobScheduleEndpoints> logger,
        CancellationToken ct)
    {
        var registration = registry.Find(jobId);
        if (registration is null) return Results.NotFound();

        if (string.IsNullOrWhiteSpace(request.CronExpression))
            return Problem("Cron expression is required.");

        // Resolve the timezone up front: an unknown id would otherwise be silently swallowed at
        // startup (UseModuleRecurringJobs falls back to the app default and logs a warning).
        var timeZone = clock.ApplicationTimeZone;
        if (!string.IsNullOrWhiteSpace(request.TimeZoneId)
            && !TimeZoneInfo.TryFindSystemTimeZoneById(request.TimeZoneId, out timeZone!))
        {
            return Problem($"Unknown time zone id '{request.TimeZoneId}'.");
        }

        var cron = request.CronExpression.Trim();

        // Apply to Hangfire BEFORE persisting. AddOrUpdate parses the cron first and throws
        // ArgumentException before touching storage, so an invalid expression leaves nothing
        // changed and we can reject it cleanly. (If the save below were to fail after a successful
        // registration, the next startup re-reads the DB row and restores the stored schedule.)
        try
        {
            if (request.IsEnabled)
                registration.Definition.Register(manager, cron, new RecurringJobOptions { TimeZone = timeZone });
            else
                manager.RemoveIfExists(jobId);
        }
        catch (ArgumentException ex)
        {
            return Problem($"Invalid cron expression '{cron}': {ex.Message}");
        }

        var updated = await registration.UpdateAsync(
            services,
            row =>
            {
                row.UpdateSchedule(cron, request.TimeZoneId);
                row.SetEnabled(request.IsEnabled);
            },
            ct);

        // The row is seeded at startup for every code-defined job, so a miss means the table was
        // cleared out from under us rather than a bad request.
        if (updated is null)
            return Results.NotFound();

        return Results.Ok(ToDto(registration, updated, clock, TryLoadRuns(storage, [jobId], clock, logger)));
    }

    /// <summary>
    /// Run info is decoration on a schedule list/update: a Hangfire storage failure must not fail the
    /// request (least of all a PUT that has already saved), so log it and send the DTOs with null run fields
    /// and RunInfoAvailable = false. Returns null on failure.
    /// </summary>
    private static Dictionary<string, JobRun>? TryLoadRuns(
        JobStorage storage, IEnumerable<string> jobIds, IDateTimeProvider clock, ILogger logger)
    {
        try
        {
            return LoadRuns(storage, jobIds, clock, logger);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read recurring-job run info from Hangfire; returning schedules without it");
            return null;
        }
    }

    /// <summary>
    /// Last/next run of each recurring job, read from Hangfire in one connection. Hangfire stores UTC;
    /// converted to application time like every other timestamp. A job Hangfire does not hold (disabled,
    /// never registered) is simply absent from the result.
    /// </summary>
    private static Dictionary<string, JobRun> LoadRuns(
        JobStorage storage, IEnumerable<string> jobIds, IDateTimeProvider clock, ILogger logger)
    {
        var runs = new Dictionary<string, JobRun>();

        using var connection = storage.GetConnection();
        foreach (var job in connection.GetRecurringJobs(jobIds.ToList()))
        {
            if (job.Removed) continue;

            // Only a Succeeded state carries PerformanceDuration, and Hangfire drops the job (and so
            // LastJobState) once it expires, so either may be null.
            var durationMs = job.LastJobState == SucceededState.StateName && job.LastJobId is not null
                ? ReadDurationMs(connection, job.LastJobId, logger)
                : null;

            runs[job.Id] = new JobRun(
                job.LastExecution is { } last ? clock.ToApplicationTime(last) : null,
                job.NextExecution is { } next ? clock.ToApplicationTime(next) : null,
                job.LastJobState,
                durationMs);
        }

        return runs;
    }

    // Its own try/catch: one corrupt state record costs that job its duration, not every job its run info.
    private static long? ReadDurationMs(IStorageConnection connection, string jobId, ILogger logger)
    {
        try
        {
            return connection.GetStateData(jobId)?.Data is { } data
                && data.TryGetValue("PerformanceDuration", out var duration)
                && long.TryParse(duration, out var ms)
                    ? ms
                    : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read the duration of recurring job run {JobId}", jobId);
            return null;
        }
    }

    private sealed record JobRun(DateTime? Last, DateTime? Next, string? LastState, long? DurationMs);

    private static IResult Problem(string detail) =>
        Results.Problem(detail: detail, statusCode: StatusCodes.Status400BadRequest);

    private static JobScheduleDto ToDto(
        JobScheduleRegistration registration,
        JobSchedule? row,
        IDateTimeProvider clock,
        Dictionary<string, JobRun>? runs)
    {
        // Mirrors the resolution in UseModuleRecurringJobs: the row wins, the code default is the
        // fallback for a missing row.
        JobRun? run = null;
        runs?.TryGetValue(registration.JobId, out run);
        var effectiveCron = row?.CronExpression ?? registration.Definition.DefaultCron;

        return new JobScheduleDto(
            registration.JobId,
            registration.Module,
            effectiveCron,
            registration.Definition.DefaultCron,
            IsOverridden: !string.Equals(
                effectiveCron, registration.Definition.DefaultCron, StringComparison.Ordinal),
            TimeZoneId: row?.TimeZoneId,
            EffectiveTimeZoneId: row?.TimeZoneId ?? clock.ApplicationTimeZone.Id,
            IsEnabled: row?.IsEnabled ?? true,
            Description: row?.Description ?? registration.Definition.Description,
            HasRow: row is not null,
            LastExecution: run?.Last,
            NextExecution: run?.Next,
            LastJobState: run?.LastState,
            LastDurationMs: run?.DurationMs,
            RunInfoAvailable: runs is not null);
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

/// <param name="EffectiveCron">The cron actually in force — the stored row, else the code default.</param>
/// <param name="IsOverridden">True when the stored cron differs from the code default.</param>
/// <param name="TimeZoneId">The stored override; null means "use the application timezone".</param>
/// <param name="HasRow">False when no override row exists yet (startup seeds one per known job).</param>
/// <param name="LastExecution">From Hangfire's recurring-job record; null when never run or the job is disabled/absent.</param>
/// <param name="NextExecution">Likewise null when disabled/absent.</param>
/// <param name="LastJobState">Null once Hangfire has expired the last job (succeeded jobs expire after about a day).</param>
/// <param name="LastDurationMs">Succeeded state's PerformanceDuration; null when unknown.</param>
/// <param name="RunInfoAvailable">False when Hangfire could not be read, so the four run fields are unknown rather than empty.</param>
public record JobScheduleDto(
    string JobId,
    string Module,
    string EffectiveCron,
    string DefaultCron,
    bool IsOverridden,
    string? TimeZoneId,
    string EffectiveTimeZoneId,
    bool IsEnabled,
    string? Description,
    bool HasRow,
    DateTime? LastExecution,
    DateTime? NextExecution,
    string? LastJobState,
    long? LastDurationMs,
    bool RunInfoAvailable);

public record UpdateJobScheduleRequest(
    string CronExpression,
    string? TimeZoneId,
    bool IsEnabled);
