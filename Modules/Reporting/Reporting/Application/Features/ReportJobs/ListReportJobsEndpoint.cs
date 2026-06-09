using Microsoft.EntityFrameworkCore;
using Reporting.Data;
using Shared.Identity;

namespace Reporting.Application.Features.ReportJobs;

/// <summary>
/// GET /reports/jobs
///
/// Owner-scoped list of the caller's recent report jobs (newest first). Used by the frontend to
/// reconcile in-flight / recently-completed jobs on app load or SignalR reconnect — so a job that
/// finished while the client was disconnected is still discoverable (alongside the durable bell
/// notification), not just via a jobId the client happened to keep.
///
/// Returns:
///   200  [{ jobId, reportTypeKey, entityId, status, requestedAt, completedAt, fileSizeBytes, errorMessage }]
///   401  unauthenticated / no resolvable bank code
/// </summary>
public sealed class ListReportJobsEndpoint : ICarterModule
{
    // Cap the window so the query never scans a user's full history; the FE only needs the
    // recent set to rehydrate chips. Bump or add paging later if a use case needs it.
    private const int MaxJobs = 50;

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/reports/jobs", HandleAsync)
            .RequireAuthorization()
            .WithTags("Reports")
            .WithName("ListReportJobs")
            .WithSummary("List the current user's recent async report jobs")
            .Produces(200)
            .Produces(401);
    }

    private static async Task<IResult> HandleAsync(
        ReportingDbContext dbContext,
        ICurrentUserService currentUserService,
        CancellationToken cancellationToken)
    {
        var userCode = currentUserService.UserCode;
        if (string.IsNullOrEmpty(userCode))
            return Results.Unauthorized();

        // Materialise first (Status is an enum mapped to a string column via the value converter;
        // calling .ToString() on it can't be translated to SQL, so stringify in memory after the query).
        var rows = await dbContext.ReportJobs
            .AsNoTracking()
            .Where(j => j.RequestedBy == userCode)
            .OrderByDescending(j => j.RequestedAt)
            .Take(MaxJobs)
            .ToListAsync(cancellationToken);

        var jobs = rows.Select(j => new
        {
            jobId = j.Id,
            reportTypeKey = j.ReportTypeKey,
            entityId = j.EntityId,
            status = j.Status.ToString(),
            requestedAt = j.RequestedAt,
            completedAt = j.CompletedAt,
            fileSizeBytes = j.FileSizeBytes,
            errorMessage = j.ErrorMessage,
        });

        return Results.Ok(jobs);
    }
}
