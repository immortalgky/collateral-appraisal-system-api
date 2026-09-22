using Appraisal.Application.Services;
using Dapper;
using Hangfire;

namespace Appraisal.Application.Features.Appraisals.RegenerateAppraisalSummary;

/// <summary>
/// POST /appraisals/{appraisalId}/documents/regenerate-summary
///
/// Admin recovery hatch for the automatic post-approval summary. Two cases need it: the render failed
/// permanently when the appraisal was approved (the APPRAISAL_COMPLETED webhook went out anyway, so LOS
/// is holding an incomplete package), or appraisal data was corrected after closing and the attached
/// summary is now wrong.
///
/// Re-running always produces a new document row; the previous one is left in place for audit.
/// GetAppraisalResult serves the newest row per DocumentTypeCode, so the fresh one wins on its own.
///
/// Side effect worth knowing about: the job re-publishes AppraisalResultReadyIntegrationEvent, which
/// fires APPRAISAL_COMPLETED to LOS a second time. That is the only way to tell them to collect again —
/// the event means "come and fetch", not "the case closed again".
///
/// No UI affordance is wired to this; it is called by hand.
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
                + "as a new D042/D043 checklist entry, and re-notifies the external system that the result "
                + "is ready.")
            .WithTags("Appraisal Documents")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> HandleAsync(
        Guid appraisalId,
        ISqlConnectionFactory connectionFactory,
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

        // force: true — skip the "already attached" check, which is the whole point of asking for a re-run.
        var jobId = backgroundJobClient.Enqueue<AppraisalSummaryAutoAttachJob>(
            j => j.RunAsync(appraisalId, row.RequestId, row.CompletedAt.Value, true, CancellationToken.None));

        logger.LogInformation(
            "Appraisal summary regeneration job {JobId} enqueued for AppraisalId={AppraisalId} by {UserCode}",
            jobId, appraisalId, currentUserService.UserCode);

        return Results.Accepted(value: new { jobId });
    }

    private sealed record AppraisalRow(Guid RequestId, string? Status, DateTime? CompletedAt);
}
