using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Time;

namespace Appraisal.Application.Services;

/// <summary>
/// Hangfire hourly job. <c>appraisal.Appraisals.SLAStatus</c> is set to OnTrack at creation and re-evaluated only
/// when work starts, so without this it never moves to AtRisk or Breached as the due date approaches and passes —
/// and the slaStatus filter, SLA sort and group counts all read that column.
///
/// One set-based UPDATE, no aggregates loaded. It touches only open appraisals (not deleted, with a due date, not
/// Completed or Cancelled — closed ones keep the status they closed with) and only rows whose status actually
/// changes, so a run writes just the transitions. The rule is <see cref="AppraisalSla"/>'s; the SQL interpolates its
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

    // {0} is @Now, bound as a parameter. The only interpolated value is a const int.
    internal static readonly string UpdateSql = $$"""
        UPDATE a
        SET a.SLAStatus = x.NewStatus
        FROM appraisal.Appraisals a
        CROSS APPLY (SELECT CASE WHEN a.SLADueDate <= {0} THEN 'Breached'
                                 WHEN a.SLADueDate <= DATEADD(day, {{AppraisalSla.AtRiskWindowDays}}, {0}) THEN 'AtRisk'
                                 ELSE 'OnTrack' END AS NewStatus) x
        WHERE a.IsDeleted = 0
          AND a.SLADueDate IS NOT NULL
          AND a.Status NOT IN ('Completed', 'Cancelled')
          AND ISNULL(a.SLAStatus, '') <> x.NewStatus
        """;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S2077:Formatting SQL queries is security-sensitive",
        Justification =
            "UpdateSql is a fixed literal whose only interpolated part is the const AppraisalSla.AtRiskWindowDays; " +
            "the one runtime value (now) is bound as the {0} parameter.")]
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var now = dateTimeProvider.ApplicationNow;

        try
        {
            var changed = await dbContext.Database.ExecuteSqlRawAsync(UpdateSql, [now], ct);
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
