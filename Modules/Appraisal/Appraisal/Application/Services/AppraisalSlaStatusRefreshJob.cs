using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Time;

namespace Appraisal.Application.Services;

/// <summary>
/// Hangfire hourly job. <c>appraisal.Appraisals.SLAStatus</c> is set once at creation (from its due date) and re-evaluated
/// only when work starts, so without this it never moves to AtRisk or Breached as the due date approaches and passes —
/// and the slaStatus filter, SLA sort and group counts all read that column.
///
/// One set-based UPDATE, no aggregates loaded. It touches only open appraisals (not deleted, with a due date, not
/// Completed or Cancelled — a closed one is settled once, when it closes, by Appraisal.Complete/Cancel, and never
/// touched again) and only rows whose status actually changes, so a run writes just the transitions. Written in
/// batches under SQL Server's 5,000-lock escalation threshold: the first run flips every past-due open row, and one
/// statement over that many rows would escalate to a table lock and block writers. The rule is <see cref="AppraisalSla"/>'s; the SQL interpolates its
/// window constant rather than repeating the number.
///
/// Deliberately does NOT stamp UpdatedAt/UpdatedBy: this is a derived recalculation, not an edit, and
/// Appraisals.UpdatedAt is surfaced to LOS (GetAppraisalStatus) as the last time the appraisal changed.
///
/// Multi-server safety: Hangfire runs a recurring job once; DisableConcurrentExecution covers a run that outlasts
/// the hour. Written through the module's own DbContext (the Workflow module must not write Appraisal tables).
/// </summary>
public class AppraisalSlaStatusRefreshJob(
    AppraisalDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<AppraisalSlaStatusRefreshJob> logger)
{
    private const string JobTag = "[APPRAISAL-SLA-REFRESH]";

    // Below the 5,000-lock escalation threshold; each batch is its own autocommit statement.
    internal const int BatchSize = 4000;

    // {0} is @Now, bound as a parameter. The interpolated parts are three compile-time constants: BatchSize,
    // AppraisalSla.AtRiskWindowDays (const ints) and AppraisalSla.ClosedStatusesSql (a const string literal).
    internal static readonly string UpdateSql = $$"""
        UPDATE TOP ({{BatchSize}}) a
        SET a.SLAStatus = x.NewStatus
        FROM appraisal.Appraisals a
        CROSS APPLY (SELECT CASE WHEN a.SLADueDate <= {0} THEN 'Breached'
                                 WHEN a.SLADueDate <= DATEADD(day, {{AppraisalSla.AtRiskWindowDays}}, {0}) THEN 'AtRisk'
                                 ELSE 'OnTrack' END AS NewStatus) x
        WHERE a.IsDeleted = 0
          AND a.SLADueDate IS NOT NULL
          AND a.Status NOT IN {{AppraisalSla.ClosedStatusesSql}}
          AND ISNULL(a.SLAStatus, '') <> x.NewStatus
        """;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S2077:Formatting SQL queries is security-sensitive",
        Justification =
            "UpdateSql is built once from compile-time constants only: BatchSize, AppraisalSla.AtRiskWindowDays and " +
            "AppraisalSla.ClosedStatusesSql, none of them caller input; the one runtime value (now) is bound as " +
            "the {0} parameter.")]
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var now = dateTimeProvider.ApplicationNow;

        try
        {
            // Same @Now and the same changed-only predicate for every batch, so rows already moved no longer
            // match and the loop ends on the first short batch.
            var changed = 0;
            int batch;
            do
            {
                batch = await dbContext.Database.ExecuteSqlRawAsync(UpdateSql, [now], ct);
                changed += batch;
            } while (batch >= BatchSize);

            logger.LogInformation("{Tag} Refreshed SLAStatus on {Count} appraisal(s) as of {Now}", JobTag, changed, now);
        }
        catch (Exception ex)
        {
            // Re-throw so Hangfire records the run as failed instead of silently marking it succeeded.
            logger.LogError(ex, "{Tag} Refresh failed", JobTag);
            throw;
        }
    }
}
