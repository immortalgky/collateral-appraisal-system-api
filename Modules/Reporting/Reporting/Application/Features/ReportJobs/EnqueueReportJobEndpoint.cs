using Hangfire;
using Reporting.Application.Services;
using Reporting.Data;
using Shared.Identity;
using Shared.Time;

namespace Reporting.Application.Features.ReportJobs;

/// <summary>
/// POST /reports/{reportTypeKey}/{entityId}/jobs
///
/// Validates the report key, creates a ReportJob row in Pending state, enqueues a Hangfire
/// background job, and returns 202 Accepted with the jobId and a Location header pointing to
/// the status endpoint.
///
/// Returns:
///   202 Accepted  { jobId }
///   404           unknown or disabled reportTypeKey
///   400           report key exists but IsEnabled = false
/// </summary>
public sealed class EnqueueReportJobEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/reports/{reportTypeKey}/{entityId}/jobs", HandleAsync)
            .RequireAuthorization()
            .WithTags("Reports")
            .WithName("EnqueueReportJob")
            .WithSummary("Enqueue an async PDF generation job for the given report and entity")
            .Produces(202)
            .Produces(400)
            .Produces(404)
            .Produces(401);
    }

    private static async Task<IResult> HandleAsync(
        string reportTypeKey,
        string entityId,
        IReportRegistry registry,
        ReportingDbContext dbContext,
        IBackgroundJobClient backgroundJobClient,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        ILogger<EnqueueReportJobEndpoint> logger,
        CancellationToken cancellationToken)
    {
        // An authenticated principal with no resolvable bank code cannot own a job (the
        // download/status endpoints are owner-scoped on UserCode) — reject up front rather
        // than bucketing it under a shared "anonymous" owner.
        var userCode = currentUserService.UserCode;
        if (string.IsNullOrEmpty(userCode))
            return Results.Unauthorized();

        var registration = registry.TryGet(reportTypeKey);
        if (registration is null)
        {
            return Results.NotFound(new { error = $"Report type '{reportTypeKey}' not found." });
        }

        if (!registration.IsEnabled)
        {
            return Results.BadRequest(new { error = $"Report type '{reportTypeKey}' is currently disabled." });
        }

        var now = dateTimeProvider.ApplicationNow;

        var job = ReportJob.Create(reportTypeKey, entityId, userCode, now);
        dbContext.ReportJobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Enqueue the Hangfire job. CancellationToken.None is intentional: the background
        // job runs outside the HTTP request lifecycle and should not be cancelled when the
        // client disconnects.
        backgroundJobClient.Enqueue<ReportGenerationJob>(j => j.RunAsync(job.Id, CancellationToken.None));

        logger.LogInformation(
            "Report job {JobId} enqueued for {ReportTypeKey}/{EntityId} by {UserCode}",
            job.Id, reportTypeKey, entityId, userCode);

        return Results.Accepted(
            $"/reports/jobs/{job.Id}",
            new { jobId = job.Id });
    }
}
