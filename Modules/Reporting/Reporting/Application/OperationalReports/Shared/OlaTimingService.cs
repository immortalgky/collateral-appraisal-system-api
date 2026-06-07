using Dapper;
using Shared.Data;
using Shared.Pagination;
using Workflow.Contracts.Sla;

namespace Reporting.Application.OperationalReports.Shared;

/// <summary>Per-appraisal inputs the OLA service needs to compute segments.</summary>
public sealed record OlaInput(Guid AppraisalId, Guid? RequestId, DateTime? AppointmentDate);

/// <summary>
/// Computed OLA durations for one appraisal, in business HOURS (weekends/holidays/lunch excluded).
/// Null when the bounding timestamps aren't both present. <see cref="ReceiveDate"/> is the
/// company→bank handoff (used as a display column on the OLA reports).
/// </summary>
public sealed class OlaTimings
{
    public decimal? OlaAppraisal { get; set; }
    public decimal? OlaInternalStaffVerify { get; set; }
    public decimal? OlaInternalChecker { get; set; }
    public decimal? OlaInternalVerify { get; set; }
    public decimal? OlaApproval { get; set; }
    public DateTime? ReceiveDate { get; set; }

    public decimal? OlaInternalStaffPlusChecker =>
        OlaInternalStaffVerify is null && OlaInternalChecker is null
            ? null
            : (OlaInternalStaffVerify ?? 0) + (OlaInternalChecker ?? 0);
}

public interface IOlaTimingService
{
    Task<IReadOnlyDictionary<Guid, OlaTimings>> ComputeAsync(
        IReadOnlyList<OlaInput> inputs, CancellationToken ct);
}

/// <summary>
/// Computes OLA segments by reading per-activity first-round timestamps from
/// <c>workflow.CompletedTasks</c> (linked to an appraisal via CorrelationId = Request.Id) and
/// measuring elapsed business time with <see cref="IBusinessTimeCalculator"/>.
///
/// NOTE — the activity-id ↔ segment mapping below is a best-effort interpretation of FSD Ch.9 and
/// SHOULD BE VALIDATED with the business. It is centralised here so it can be corrected in one place.
///
/// ASSUMPTION — workflow correlation is per REQUEST (CorrelationId = Request.Id), and these reports
/// assume one appraisal per request. Appraisals.RequestId is NOT unique, so if a request ever owns
/// multiple appraisals (e.g. CI/Appeal flows) they would each read the same CompletedTasks and get
/// identical OLA segments. Narrow the correlation to the appraisal (carry AppraisalId on the task
/// stream) before relying on OLA reports for such multi-appraisal requests.
/// </summary>
internal sealed class OlaTimingService(
    ISqlConnectionFactory connectionFactory,
    IBusinessTimeCalculator businessTime) : IOlaTimingService
{
    // First-round activity completions that bound each OLA segment.
    private const string ExtExecution = "ext-appraisal-execution";
    private const string ExtVerification = "ext-appraisal-verification";
    private const string IntExecution = "int-appraisal-execution";
    private const string IntCheck = "int-appraisal-check";
    private const string IntVerification = "int-appraisal-verification";
    private const string Approval = "pending-approval";

    public async Task<IReadOnlyDictionary<Guid, OlaTimings>> ComputeAsync(
        IReadOnlyList<OlaInput> inputs, CancellationToken ct)
    {
        var result = new Dictionary<Guid, OlaTimings>();
        var correlationIds = inputs.Where(i => i.RequestId.HasValue).Select(i => i.RequestId!.Value).Distinct().ToList();
        if (correlationIds.Count == 0)
        {
            foreach (var i in inputs) result[i.AppraisalId] = new OlaTimings();
            return result;
        }

        // Load every completed activity for these correlations; reduce to first-round per activity.
        // Chunk the IN list to stay under SQL Server's 2100-parameter limit on large exports.
        var rows = new List<TaskRow>();
        foreach (var batch in correlationIds.Chunk(1000))
        {
            var batchRows = await connectionFactory.QueryAsync<TaskRow>(
                """
                SELECT CorrelationId, ActivityId, AssignedAt, CompletedAt
                FROM workflow.CompletedTasks
                WHERE CorrelationId IN @CorrelationIds AND ActivityId IS NOT NULL
                """,
                new { CorrelationIds = batch });
            rows.AddRange(batchRows);
        }

        // CorrelationId -> ActivityId -> earliest (first-round) assigned/completed timestamps.
        var byCorrelation = rows
            .GroupBy(r => r.CorrelationId)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(r => r.ActivityId!)
                      .ToDictionary(
                          a => a.Key,
                          a => a.OrderBy(r => r.AssignedAt).First(),
                          StringComparer.OrdinalIgnoreCase));

        foreach (var input in inputs)
        {
            var timings = new OlaTimings();
            result[input.AppraisalId] = timings;

            if (input.RequestId is null || !byCorrelation.TryGetValue(input.RequestId.Value, out var acts))
                continue;

            DateTime? Assigned(string a) => acts.TryGetValue(a, out var t) ? t.AssignedAt : null;
            DateTime? Completed(string a) => acts.TryGetValue(a, out var t) ? t.CompletedAt : null;

            var companySentToBank = Completed(ExtVerification);
            timings.ReceiveDate = companySentToBank;

            // OLA Appraisal: appointment → company-sent-to-bank (external) or internal execution complete.
            timings.OlaAppraisal = await HoursBetween(input.AppointmentDate, companySentToBank ?? Completed(IntExecution), ct);
            // OLA Internal Staff (Verify): company-sent-to-bank → internal staff sent to checker.
            timings.OlaInternalStaffVerify = await HoursBetween(companySentToBank, Assigned(IntCheck), ct);
            // OLA Internal Checker: assign-to-checker → sent-to-verify.
            timings.OlaInternalChecker = await HoursBetween(Assigned(IntCheck), Assigned(IntVerification), ct);
            // OLA Internal Verify: assign-to-verify → sent-to-approval.
            timings.OlaInternalVerify = await HoursBetween(Assigned(IntVerification), Assigned(Approval), ct);
            // OLA Approval: assign-to-approval → approval complete.
            timings.OlaApproval = await HoursBetween(Assigned(Approval), Completed(Approval), ct);
        }

        return result;
    }

    private async Task<decimal?> HoursBetween(DateTime? from, DateTime? to, CancellationToken ct)
    {
        if (from is null || to is null || to <= from) return null;
        var minutes = await businessTime.GetBusinessMinutesBetweenAsync(from.Value, to.Value, ct);
        return Math.Round(minutes / 60m, 2);
    }

    private sealed record TaskRow(Guid CorrelationId, string? ActivityId, DateTime AssignedAt, DateTime CompletedAt);
}
