using Carter;
using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Shared.Scheduling;
using Shared.Time;

namespace Common.Application.Features.JobSchedules;

/// <summary>
/// Admin maintenance for the per-module <c>{schema}.JobSchedules</c> tables — the rows that override
/// a Hangfire recurring job's cron, timezone and enabled state.
///
/// Rows live in nine module schemas, so this reads and writes through
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

    private static async Task<IResult> List(
        IJobScheduleRegistry registry,
        IServiceProvider services,
        IDateTimeProvider clock,
        CancellationToken ct)
    {
        var result = new List<JobScheduleDto>();

        foreach (var registration in registry.All)
        {
            var row = await registration.LoadAsync(services, ct);
            result.Add(ToDto(registration, row, clock));
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> Update(
        string jobId,
        UpdateJobScheduleRequest request,
        IJobScheduleRegistry registry,
        IRecurringJobManager manager,
        IServiceProvider services,
        IDateTimeProvider clock,
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

        return Results.Ok(ToDto(registration, updated, clock));
    }

    private static IResult Problem(string detail) =>
        Results.Problem(detail: detail, statusCode: StatusCodes.Status400BadRequest);

    private static JobScheduleDto ToDto(
        JobScheduleRegistration registration,
        JobSchedule? row,
        IDateTimeProvider clock)
    {
        // Mirrors the resolution in UseModuleRecurringJobs: the row wins, the code default is the
        // fallback for a missing row.
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
            HasRow: row is not null);
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

/// <param name="EffectiveCron">The cron actually in force — the stored row, else the code default.</param>
/// <param name="IsOverridden">True when the stored cron differs from the code default.</param>
/// <param name="TimeZoneId">The stored override; null means "use the application timezone".</param>
/// <param name="HasRow">False when no override row exists yet (startup seeds one per known job).</param>
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
    bool HasRow);

public record UpdateJobScheduleRequest(
    string CronExpression,
    string? TimeZoneId,
    bool IsEnabled);
