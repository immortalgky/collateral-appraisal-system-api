using Reporting.Data;
using Shared.Identity;

namespace Reporting.Application.Features.ReportJobs;

/// <summary>
/// GET /reports/jobs/{jobId}/download
///
/// Streams the generated PDF artifact to the caller.
///
/// IDOR guard: same owner-only check as GetReportJobStatusEndpoint — returns 404 for unknown
/// or foreign jobs rather than 403 so the existence of the jobId is not leaked.
///
/// Path safety: the StoragePath used to read the file is exclusively the value stored in the
/// database row (a Guid-named file written by ReportGenerationJob). No client-supplied path is
/// accepted.
///
/// Multi-node note: if the artifact is missing on disk, the response is 410 Gone. In Local mode
/// (node-local wwwroot) this can happen when the download request lands on a different IIS node
/// than the one that wrote the file. Configure FileStorage:Mode=Nas with a shared NasBasePath
/// so all nodes can access the same artifact store.
///
/// Returns:
///   200  application/pdf stream
///   404  job not found, or belongs to a different user
///   409  job not yet completed
///   410  artifact missing from disk (cleaned up or written on a different node in Local mode)
///   401  unauthenticated
/// </summary>
public sealed class DownloadReportJobEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/reports/jobs/{jobId:guid}/download", HandleAsync)
            .RequireAuthorization()
            .WithTags("Reports")
            .WithName("DownloadReportJob")
            .WithSummary("Download the generated PDF for a completed async report job")
            .Produces(200, contentType: "application/pdf")
            .Produces(404)
            .Produces(409)
            .Produces(410)
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

        // IDOR guard: 404 for unknown OR foreign jobs (owner-only).
        if (job is null || job.RequestedBy != userCode)
        {
            return Results.NotFound(new { error = "Report job not found." });
        }

        if (job.Status != ReportJobStatus.Completed)
        {
            return Results.Conflict(new
            {
                error = $"Report job is not yet completed (current status: {job.Status})."
            });
        }

        // StoragePath is always an absolute path written by ReportGenerationJob — never
        // client-supplied, so path traversal is not a concern here.
        if (string.IsNullOrEmpty(job.StoragePath) || !File.Exists(job.StoragePath))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status410Gone,
                title: "Artifact not found",
                detail: "The report artifact is no longer available on this server. " +
                        "It may have been cleaned up by the retention policy, " +
                        "or in a multi-node (Local storage) deployment it may reside on a different node. " +
                        "Please re-enqueue the report job.");
        }

        var fileName = job.FileName ?? $"{job.ReportTypeKey}.pdf";

        // Stream straight from disk rather than buffering the whole PDF into memory — reports
        // can approach the 100 MB attachment cap, and ReadAllBytes would pin that on the LOH
        // per concurrent download. A seekable FileStream supports range requests and is disposed
        // by Results.File after the response is sent. StoragePath may be an absolute/UNC (NAS)
        // path, so we open the stream ourselves rather than using the web-root-relative overload.
        var stream = new FileStream(
            job.StoragePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            useAsync: true);

        return Results.File(
            stream,
            contentType: "application/pdf",
            fileDownloadName: fileName,
            enableRangeProcessing: true);
    }
}
