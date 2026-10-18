using System.Text.Json;
using Appraisal.Application.Features.Appraisals.NotifyExternalSystem;
using Appraisal.Application.Services;
using Appraisal.Infrastructure;
using Dapper;
using Hangfire;
using Shared.Time;

namespace Appraisal.Application.Features.Appraisals.RegenerateAppraisalSummary;

/// <summary>
/// POST /appraisals/{appraisalId}/documents/regenerate-summary
///
/// Admin recovery hatch for the automatic post-approval summary. Two cases need it: the render failed
/// permanently when the appraisal was approved (the APPRAISAL_COMPLETED webhook went out anyway, so LOS
/// is holding an incomplete package), or appraisal data was corrected after closing and the attached
/// summary is now wrong.
///
/// Re-running always produces a new document row; the previous one is left in place as history.
/// GetAppraisalResult sends every attached copy, newest first, each with its FileName and UploadedAt, so
/// LOS can tell the fresh one from the earlier ones. Only summaries generated from this change on carry a
/// timestamp in the file name; UploadedAt is what orders them.
///
/// Side effect worth knowing about: the job re-publishes AppraisalResultReadyIntegrationEvent, which
/// fires APPRAISAL_COMPLETED to LOS a second time. That is the only way to tell them to collect again —
/// the event means "come and fetch", not "the case closed again". The body may carry
/// notifyExternal: false to attach the new summary without telling the source system; absent or null
/// means true, so a bare call behaves as it always has. When notifying an appraisal that has an external
/// source, the history row also carries an "ExternalNotification" entry naming the system
/// ("ExternalNotificationSkipped" when notifyExternal is false).
///
/// The data-correction page calls this with a body { reason }. A non-empty reason is written to the same
/// correction history as property and document corrections (key "AppraisalSummary") before the job is
/// queued. Without a body it behaves as before and leaves no history row. Auth stays login-only on
/// purpose (recorded decision), so the history row is the only attribution for a UI-driven regeneration.
///
/// Returns:
///   202 Accepted  { jobId }
///   404           unknown appraisal
///   409           appraisal is not Completed — there is no committee decision to report yet
/// </summary>
public class RegenerateAppraisalSummaryEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/appraisals/{appraisalId:guid}/documents/regenerate-summary", HandleAsync)
            .RequireAuthorization()
            .WithName("RegenerateAppraisalSummary")
            .WithSummary("Re-generate and attach the post-approval Appraisal Summary")
            .WithDescription(
                "Admin recovery action. Renders the Appraisal Summary again, attaches it to the appraisal "
                + "as a new D042/D043 checklist entry, and (unless notifyExternal is false) re-notifies the "
                + "external system that the result is ready.")
            .WithTags("Appraisal Documents")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> HandleAsync(
        Guid appraisalId,
        RegenerateAppraisalSummaryRequest? request,
        ISqlConnectionFactory connectionFactory,
        AppraisalDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        IBackgroundJobClient backgroundJobClient,
        ILogger<RegenerateAppraisalSummaryEndpoint> logger,
        ICurrentUserService currentUserService,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT a.[RequestId], a.[Status], a.[CompletedAt]
            FROM [appraisal].[Appraisals] a
            WHERE a.[Id] = @AppraisalId
              AND a.[IsDeleted] = 0
            """;

        using var connection = connectionFactory.CreateNewConnection();
        var row = await connection.QuerySingleOrDefaultAsync<AppraisalRow>(
            new CommandDefinition(sql, new { AppraisalId = appraisalId }, cancellationToken: cancellationToken));

        if (row is null)
            return Results.NotFound(new { error = $"Appraisal '{appraisalId}' not found." });

        // CompletedAt is what the committee-approval handler stamps alongside Status; without it there is
        // no approval evidence for the summary to carry, and no timestamp to measure "post-completion" against.
        if (!string.Equals(row.Status?.Trim(), "Completed", StringComparison.OrdinalIgnoreCase)
            || row.CompletedAt is null)
        {
            return Results.Problem(
                title: "AppraisalNotCompleted",
                statusCode: StatusCodes.Status409Conflict,
                detail: $"Appraisal is '{row.Status}'; the summary can only be regenerated after committee approval.",
                extensions: new Dictionary<string, object?> { ["errorCode"] = "APPRAISAL_NOT_COMPLETED" });
        }

        // Longer than the column would fail the save with a 500; refuse it up front instead.
        if (request?.Reason is { Length: > 4000 })
            return Results.Problem(
                title: "ReasonTooLong",
                statusCode: StatusCodes.Status400BadRequest,
                detail: "Reason must be 4000 characters or fewer.",
                extensions: new Dictionary<string, object?> { ["errorCode"] = "REASON_TOO_LONG" });

        // A reason key that is present but blank is a client bug, not the manual no-body recovery call:
        // regenerating would notify LOS again with nobody on record, so refuse it.
        if (request?.Reason is not null && string.IsNullOrWhiteSpace(request.Reason))
            return Results.Problem(
                title: "ReasonRequired",
                statusCode: StatusCodes.Status400BadRequest,
                detail: "Reason must not be blank. Omit it entirely for an unattributed regeneration.",
                extensions: new Dictionary<string, object?> { ["errorCode"] = "REASON_REQUIRED" });

        var notifyExternal = request?.NotifyExternal ?? true;

        // Choosing NOT to tell the source system is a deliberate act and must leave a trace; without a reason
        // there would be no history row at all. (No body keeps the old unattributed-but-notifying behaviour.)
        if (!notifyExternal && string.IsNullOrWhiteSpace(request?.Reason))
            return Results.Problem(
                title: "ReasonRequired",
                statusCode: StatusCodes.Status400BadRequest,
                detail: "A reason is required when regenerating without notifying the source system.",
                extensions: new Dictionary<string, object?> { ["errorCode"] = "REASON_REQUIRED" });

        // Saved before the enqueue so a failed save cannot leave a regeneration that has no history row.
        if (!string.IsNullOrWhiteSpace(request?.Reason))
        {
            var changes = new Dictionary<string, object?>
            {
                ["AppraisalSummary"] = new { from = (string?)null, to = "Regeneration requested" },
            };
            // Record the choice either way when there is a source system: a later reader must be able to tell
            // "deliberately not told" apart from an appraisal that had nobody to tell.
            if ((await AppraisalExternalSourceQuery.GetAsync(connection, appraisalId, cancellationToken))
                ?.ExternalSystem is { } externalSystem)
            {
                changes[notifyExternal ? "ExternalNotification" : "ExternalNotificationSkipped"] =
                    new { from = (string?)null, to = externalSystem };
            }

            dbContext.AppraisalPropertyCorrectionLogs.Add(AppraisalPropertyCorrectionLog.ForDocuments(
                appraisalId,
                JsonSerializer.Serialize(changes),
                request.Reason.Trim(),
                currentUserService.UserCode ?? currentUserService.Username ?? "unknown",
                dateTimeProvider.ApplicationNow));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // force: true — skip the "already attached" check, which is the whole point of asking for a re-run.
        var jobId = backgroundJobClient.Enqueue<AppraisalSummaryAutoAttachJob>(
            j => j.RunAsync(appraisalId, row.RequestId, row.CompletedAt.Value, true, notifyExternal, CancellationToken.None));

        logger.LogInformation(
            "Appraisal summary regeneration job {JobId} enqueued for AppraisalId={AppraisalId} by {UserCode}",
            jobId, appraisalId, currentUserService.UserCode);

        return Results.Accepted(value: new { jobId });
    }

    /// <summary>
    /// Optional body; the reason is what makes the regeneration show up in the correction history.
    /// NotifyExternal null (or no body) means true.
    /// </summary>
    public sealed record RegenerateAppraisalSummaryRequest(string? Reason, bool? NotifyExternal = null);

    private sealed record AppraisalRow(Guid RequestId, string? Status, DateTime? CompletedAt);
}
