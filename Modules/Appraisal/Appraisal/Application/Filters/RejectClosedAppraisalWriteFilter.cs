using Appraisal.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace Appraisal.Application.Filters;

/// <summary>
/// Rejects HTTP writes to property data on a Completed or Cancelled appraisal.
///
/// Why this exists: the "closed appraisals are read-only" rule lived entirely in the frontend —
/// the API never looked at appraisal.Status before writing, and the property endpoints carry no
/// permission beyond "authenticated". Anyone posting directly could edit a closed appraisal with
/// no reason recorded and no audit row, which would make the data-correction feature's audit trail
/// meaningless.
///
/// Why an endpoint filter rather than a MediatR behavior or an aggregate guard: this must catch
/// HTTP callers ONLY. Integration-event consumers, Hangfire jobs and the workflow engine
/// legitimately mutate closed appraisals (SetExternalSyncStatus on a Completed appraisal is normal
/// today), and a pipeline-level or domain-level guard would have to thread a "system" bypass flag
/// through every one of those call sites. An endpoint filter cannot see them at all.
///
/// The sanctioned way in — CorrectPropertyDataEndpoint — simply does not add this filter.
/// </summary>
public sealed class RejectClosedAppraisalWriteFilter : IEndpointFilter
{
    /// <summary>Set false to disable the guard without redeploying, if it ever blocks a real flow.</summary>
    public const string EnabledConfigKey = "Appraisal:BlockWritesOnClosedAppraisals";

    private static readonly string[] ClosedStatuses = ["Completed", "Cancelled"];

    // NOTE: parameterless on purpose. AddEndpointFilter<T>() instantiates the filter from the ROOT
    // service provider when the endpoint is built, so constructor-injecting the scoped
    // AppraisalDbContext would capture it (or throw). Everything is resolved per request below.
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var services = http.RequestServices;

        var enabled = services.GetRequiredService<IConfiguration>().GetValue(EnabledConfigKey, true);
        if (!enabled) return await next(context);

        if (!http.Request.RouteValues.TryGetValue("appraisalId", out var rawId)
            || !Guid.TryParse(rawId?.ToString(), out var appraisalId))
        {
            // No appraisal in the route — nothing to check. Let it through rather than guess.
            return await next(context);
        }

        var dbContext = services.GetRequiredService<AppraisalDbContext>();

        var status = await dbContext.Appraisals
            .AsNoTracking()
            .Where(a => a.Id == appraisalId)
            .Select(a => a.Status.Code)
            .FirstOrDefaultAsync(http.RequestAborted);

        if (status is null || !ClosedStatuses.Contains(status))
            return await next(context);

        services.GetRequiredService<ILogger<RejectClosedAppraisalWriteFilter>>()
            .LogWarning(
                "[CLOSED-APPRAISAL] Blocked {Method} {Path} on appraisal {AppraisalId} (status {Status})",
                http.Request.Method, http.Request.Path, appraisalId, status);

        return Results.Problem(
            title: "AppraisalClosed",
            statusCode: StatusCodes.Status409Conflict,
            detail: $"Appraisal is {status}; property data cannot be modified here. " +
                    "Use the appraisal data-correction screen, which records a reason and an audit entry.",
            extensions: new Dictionary<string, object?> { ["errorCode"] = "APPRAISAL_CLOSED" });
    }
}
