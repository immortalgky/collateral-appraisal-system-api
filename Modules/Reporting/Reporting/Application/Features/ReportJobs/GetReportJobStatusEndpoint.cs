using Reporting.Data;
using Shared.Identity;

namespace Reporting.Application.Features.ReportJobs;

/// <summary>
/// GET /reports/jobs/{jobId}
///
/// Returns the current status and metadata for one report job.
///
/// IDOR guard: the job is only returned if it belongs to the calling user (RequestedBy == UserCode).
/// Returning 404 (rather than 403) for foreign jobs avoids leaking that the jobId exists at all.
///
/// Returns:
///   200  { jobId, reportTypeKey, entityId, status, requestedAt, startedAt, completedAt, fileSizeBytes, durationMs, errorMessage }
///   404  job not found, or belongs to a different user
///   401  unauthenticated
/// </summary>
public sealed class GetReportJobStatusEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/reports/jobs/{jobId:guid}", HandleAsync)
            .RequireAuthorization()
            .WithTags("Reports")
            .WithName("GetReportJobStatus")
            .WithSummary("Get the status of an async report generation job")
            .Produces(200)
            .Produces(404)
            .Produces(401);
    }

    private static async Task<IResult> HandleAsync(
        Guid jobId,
        ReportingDbContext dbContext,
        ICurrentUserService currentUserService,
        CancellationToken cancellationToken)
    {
        // An authenticated principal with no resolvable bank code can neither own nor read a
        // job — reject rather than bucketing it under a shared "anonymous" owner.
        var userCode = currentUserService.UserCode;
        if (string.IsNullOrEmpty(userCode))
            return Results.Unauthorized();

        var job = await dbContext.ReportJobs.FindAsync([jobId], cancellationToken);

        // Return 404 for unknown jobs AND for jobs that belong to a different user —
        // avoids leaking that another user's jobId exists (IDOR guard, owner-only).
        if (job is null || job.RequestedBy != userCode)
        {
            return Results.NotFound(new { error = "Report job not found." });
        }

        return Results.Ok(new
        {
            jobId = job.Id,
            reportTypeKey = job.ReportTypeKey,
            entityId = job.EntityId,
            status = job.Status.ToString(),
            requestedAt = job.RequestedAt,
            startedAt = job.StartedAt,
            completedAt = job.CompletedAt,
            fileSizeBytes = job.FileSizeBytes,
            durationMs = job.DurationMs,
            errorMessage = job.ErrorMessage,
        });
    }
}
