namespace Workflow.Contracts.Sla;

/// <summary>
/// Shared helpers for turning <see cref="IBusinessTimeCalculator"/> results into the
/// list-page "Elapsed" / "Remaining" columns. Centralised so every task/appraisal list
/// surface that reads a SQL view (which deliberately no longer computes these via calendar
/// <c>DATEDIFF</c>) produces identical business-hour values and overdue semantics.
/// </summary>
public static class BusinessTimeCalculatorExtensions
{
    /// <summary>
    /// Computes the Elapsed and Remaining columns in whole business hours, excluding
    /// weekends, holidays and lunch (per <see cref="IBusinessTimeCalculator"/>).
    /// <list type="bullet">
    /// <item>Elapsed = business hours from <paramref name="start"/> to <paramref name="now"/>
    /// (null when <paramref name="start"/> is null).</item>
    /// <item>Remaining = business hours from <paramref name="now"/> to <paramref name="due"/>,
    /// negative when overdue (now &gt; due); null when <paramref name="due"/> is null.</item>
    /// </list>
    /// </summary>
    /// <param name="clockStart">
    /// Optional SLA clock-start anchor (e.g. <c>PendingTask.SlaStartAt</c>). When the deadline is
    /// anchored ahead of <paramref name="now"/> (a future appointment), Remaining is computed from
    /// <c>max(now, clockStart)</c> so it equals the budget (<c>businessHours(clockStart → due)</c>)
    /// instead of <c>wait-until-clock-start + budget</c>. Null preserves the legacy behaviour.
    /// </param>
    public static async Task<(int? ElapsedHours, int? RemainingHours)> ComputeElapsedRemainingHoursAsync(
        this IBusinessTimeCalculator calculator,
        DateTime now,
        DateTime? start,
        DateTime? due,
        DateTime? clockStart = null,
        CancellationToken ct = default)
    {
        int? elapsed = start is { } from
            ? (int)Math.Round(await calculator.GetBusinessMinutesBetweenAsync(from, now, ct) / 60.0)
            : null;

        int? remaining = null;
        if (due is { } dueAt)
        {
            // Clamp "now" up to the clock-start so Remaining never exceeds the configured budget.
            var effectiveNow = clockStart is { } cs && now < cs ? cs : now;
            // GetBusinessMinutesBetweenAsync returns 0 when from >= to, so the overdue case
            // must swap the arguments and negate to surface the hours past due.
            remaining = effectiveNow <= dueAt
                ? (int)Math.Round(await calculator.GetBusinessMinutesBetweenAsync(effectiveNow, dueAt, ct) / 60.0)
                : -(int)Math.Round(await calculator.GetBusinessMinutesBetweenAsync(dueAt, effectiveNow, ct) / 60.0);
        }

        return (elapsed, remaining);
    }
}
