using Dapper;
using Shared.Data;
using Shared.Pagination;
using Workflow.Contracts.Sla; // IBusinessTimeCalculator

namespace Reporting.Application.OperationalReports.Shared;

// ---------------------------------------------------------------------------
// Public result types
// ---------------------------------------------------------------------------

/// <summary>Per-cycle slice of party SLA measurement.</summary>
public record PartyCycle(
    int CycleIndex,
    bool IsRework,
    DateTime CycleStart,
    DateTime CycleEnd,
    decimal BusinessMinutes,
    bool BudgetMet,
    decimal? BudgetMinutes);

/// <summary>Aggregated party SLA result (vendor or bank).</summary>
public record PartySlaParty(
    IReadOnlyList<PartyCycle> Cycles,
    decimal TotalBusinessMinutes,
    bool CumulativeBudgetMet,
    decimal? BudgetMinutes);

/// <summary>Full party-SLA evaluation result for one appraisal.</summary>
public record PartySlaResult(PartySlaParty Vendor, PartySlaParty Bank);

// ---------------------------------------------------------------------------
// Service
// ---------------------------------------------------------------------------

public interface IPartySlaEvaluator
{
    /// <summary>
    /// Evaluates vendor and bank elapsed business time for <paramref name="correlationId"/>
    /// (= Appraisal.RequestId) against the configured Stage-scope SLA budgets.
    /// Returns null when no completed tasks exist for this correlation.
    /// </summary>
    Task<PartySlaResult?> EvaluateAsync(
        Guid correlationId,
        Guid? workflowDefinitionId,
        Guid? companyId,
        string? loanType,
        string? appraisalType,
        CancellationToken ct = default);
}

/// <summary>
/// On-read party-SLA evaluator for vendor and bank segments.
///
/// Party definition:
///   Vendor  — any activity whose ID starts with "ext-appraisal" (external vendor cycle)
///   Bank    — all other activities in the execution stream
///
/// Cycle definition:
///   A maximal run of consecutive completed-task legs for the same party.
///   Leg eligibility: ActivityId is not null AND ActionTaken != "Reassigned".
///   IsRework = true for all cycles after the first (cycleIndex > 1 for that party).
///
/// Business-time is measured with <see cref="IBusinessTimeCalculator"/> so weekends,
/// holidays, and lunch breaks are excluded.
///
/// Budgets are loaded from the Stage-scope SlaPolicy rows (vendor Stage = ext-appraisal-execution
/// window; bank Stage = int-appraisal-execution window).
/// </summary>
internal sealed class PartySlaEvaluator(
    ISqlConnectionFactory connectionFactory,
    IBusinessTimeCalculator businessTime) : IPartySlaEvaluator
{
    // Activity ID prefix that denotes vendor (external company) work.
    private const string VendorPrefix = "ext-appraisal";
    // Stage startActivityKey → budget source for vendor window.
    private const string VendorStageKey = "ext-appraisal-execution";
    // Stage startActivityKey → budget source for bank in-house window.
    private const string BankStageKey = "int-appraisal-execution";

    public async Task<PartySlaResult?> EvaluateAsync(
        Guid correlationId,
        Guid? workflowDefinitionId,
        Guid? companyId,
        string? loanType,
        string? appraisalType,
        CancellationToken ct = default)
    {
        // Load all eligible legs for this correlation, ordered by AssignedAt.
        // Exclude "Reassigned" legs (audit-only, not real execution segments).
        var legs = (await connectionFactory.QueryAsync<TaskLeg>(
            """
            SELECT
                ActivityId,
                AssignedAt,
                CompletedAt,
                ActionTaken
            FROM workflow.CompletedTasks
            WHERE CorrelationId = @CorrelationId
              AND ActivityId IS NOT NULL
              AND (ActionTaken IS NULL OR ActionTaken <> 'Reassigned')
            ORDER BY AssignedAt
            """,
            new DynamicParameters(new { CorrelationId = correlationId })
        )).ToList();

        if (legs.Count == 0) return null;

        // Classify each leg as Vendor or Bank.
        var classified = legs.Select(l => new
        {
            l.ActivityId,
            l.AssignedAt,
            l.CompletedAt,
            IsVendor = l.ActivityId!.StartsWith(VendorPrefix, StringComparison.OrdinalIgnoreCase)
        }).ToList();

        // Split into consecutive same-party runs (cycles).
        // A new cycle starts whenever the party owner changes in the ordered sequence.
        var vendorCycleBuckets = new List<List<(DateTime AssignedAt, DateTime CompletedAt)>>();
        var bankCycleBuckets   = new List<List<(DateTime AssignedAt, DateTime CompletedAt)>>();
        bool? lastIsVendor = null;
        foreach (var leg in classified)
        {
            if (lastIsVendor is null || lastIsVendor != leg.IsVendor)
            {
                if (leg.IsVendor)
                    vendorCycleBuckets.Add(new List<(DateTime, DateTime)>());
                else
                    bankCycleBuckets.Add(new List<(DateTime, DateTime)>());
            }
            var bucket = leg.IsVendor ? vendorCycleBuckets[^1] : bankCycleBuckets[^1];
            bucket.Add((leg.AssignedAt, leg.CompletedAt));
            lastIsVendor = leg.IsVendor;
        }

        // H1: Vendor window is appointment-anchored (appointment date → ext-appraisal-verification).
        // Clip each vendor cycle's start to max(AssignedAt, appointmentDate) so time spent waiting
        // before the visit date is excluded from the vendor SLA measurement.
        var appointmentDate = await GetLatestNonCancelledAppointmentDateAsync(correlationId, ct);
        if (appointmentDate.HasValue && vendorCycleBuckets.Count > 0)
        {
            foreach (var bucket in vendorCycleBuckets)
            {
                if (bucket.Count > 0 && bucket[0].AssignedAt < appointmentDate.Value)
                    bucket[0] = (appointmentDate.Value, bucket[0].CompletedAt);
            }
        }

        // Load stage budgets via direct Dapper query (raw DurationHours, not calendar-computed).
        var vendorBudgetMinutes = await GetStageBudgetMinutesAsync(
            workflowDefinitionId, VendorStageKey, companyId, loanType, appraisalType, ct);
        var bankBudgetMinutes = await GetStageBudgetMinutesAsync(
            workflowDefinitionId, BankStageKey, companyId, loanType, appraisalType, ct);

        var vendorParty = await BuildPartyAsync(vendorCycleBuckets, vendorBudgetMinutes, ct);
        var bankParty   = await BuildPartyAsync(bankCycleBuckets,   bankBudgetMinutes,   ct);

        return new PartySlaResult(vendorParty, bankParty);
    }

    /// <summary>
    /// Returns the most-recently-scheduled non-cancelled appointment date for this appraisal.
    /// Returns null when no confirmed appointment exists (e.g. in-house workflow).
    /// </summary>
    private async Task<DateTime?> GetLatestNonCancelledAppointmentDateAsync(
        Guid correlationId, CancellationToken ct)
    {
        var rows = await connectionFactory.QueryAsync<DateTime?>(
            """
            SELECT TOP 1 a.AppointmentDateTime
            FROM appraisal.Appointments a
            INNER JOIN appraisal.AppraisalAssignments aa ON aa.Id = a.AssignmentId
            INNER JOIN appraisal.Appraisals ap ON ap.Id = aa.AppraisalId
            WHERE ap.RequestId = @CorrelationId
              AND a.Status <> 'Cancelled'
            ORDER BY a.CreatedOn DESC
            """,
            new DynamicParameters(new { CorrelationId = correlationId }));

        return rows.FirstOrDefault();
    }

    private async Task<PartySlaParty> BuildPartyAsync(
        List<List<(DateTime AssignedAt, DateTime CompletedAt)>> cycleBuckets,
        decimal? budgetMinutes,
        CancellationToken ct)
    {
        if (cycleBuckets.Count == 0)
            return new PartySlaParty(Array.Empty<PartyCycle>(), 0m, budgetMinutes is null, budgetMinutes);

        // M1: Each cycle is compared against the FULL stage budget — we do NOT divide evenly.
        // Rework doesn't grant a fresh window; the cumulative total is what matters for SLA met.
        var resultCycles = new List<PartyCycle>();
        decimal totalMinutes = 0m;

        for (int i = 0; i < cycleBuckets.Count; i++)
        {
            var bucket = cycleBuckets[i];
            if (bucket.Count == 0) continue;

            var cycleStart = bucket[0].AssignedAt;
            var cycleEnd   = bucket[^1].CompletedAt;

            // Business minutes from the first leg's AssignedAt to the last leg's CompletedAt.
            var minutes = (decimal)await businessTime.GetBusinessMinutesBetweenAsync(cycleStart, cycleEnd, ct);

            // BudgetMet per cycle = did this cycle alone stay within the full stage budget?
            // Useful to highlight which rework rounds are individually oversized.
            bool budgetMet = budgetMinutes is null || minutes <= budgetMinutes.Value;
            bool isRework  = i > 0; // cycle index > 1 (zero-based here) = rework

            resultCycles.Add(new PartyCycle(
                CycleIndex: i + 1,
                IsRework: isRework,
                CycleStart: cycleStart,
                CycleEnd: cycleEnd,
                BusinessMinutes: minutes,
                BudgetMet: budgetMet,
                BudgetMinutes: budgetMinutes));

            totalMinutes += minutes;
        }

        bool cumulativeMet = budgetMinutes is null || totalMinutes <= budgetMinutes.Value;
        return new PartySlaParty(resultCycles, totalMinutes, cumulativeMet, budgetMinutes);
    }

    /// <summary>
    /// Loads the raw DurationHours for a Stage-scope SlaPolicy and converts to minutes.
    /// Returns null when no matching policy row exists (no budget defined).
    /// A 0-hour policy row is preserved as 0 minutes (always-exceeded budget).
    /// </summary>
    private async Task<decimal?> GetStageBudgetMinutesAsync(
        Guid? workflowDefinitionId,
        string startActivityKey,
        Guid? companyId,
        string? loanType,
        string? appraisalType,
        CancellationToken ct)
    {
        // Query directly — ISlaCalculatorClient returns DueAt, not raw hours.
        // Using int? so we can distinguish a missing row (null) from a 0-hour policy row.
        var rows = await connectionFactory.QueryAsync<int?>(
            """
            SELECT TOP 1 DurationHours
            FROM workflow.SlaPolicies
            WHERE Scope = 2 -- Stage
              AND StartActivityKey = @StartActivityKey
              AND (WorkflowDefinitionId IS NULL OR WorkflowDefinitionId = @WorkflowDefinitionId)
              AND (LoanType IS NULL OR LoanType = @LoanType)
              AND (AppraisalType IS NULL OR AppraisalType = @AppraisalType)
              AND (CompanyId IS NULL OR CompanyId = @CompanyId)
            ORDER BY Priority ASC,
              CASE WHEN CompanyId IS NOT NULL THEN 0 ELSE 1 END,
              CASE WHEN LoanType IS NOT NULL THEN 0 ELSE 1 END,
              CASE WHEN AppraisalType IS NOT NULL THEN 0 ELSE 1 END
            """,
            new DynamicParameters(new
            {
                StartActivityKey = startActivityKey,
                WorkflowDefinitionId = workflowDefinitionId,
                LoanType = loanType,
                AppraisalType = appraisalType,
                CompanyId = companyId
            }));

        var durationHours = rows.FirstOrDefault();
        return durationHours.HasValue ? (decimal)(durationHours.Value * 60) : null;
    }

    private sealed record TaskLeg(string? ActivityId, DateTime AssignedAt, DateTime CompletedAt, string? ActionTaken);
}
