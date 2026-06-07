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
    public static async Task<(int? ElapsedHours, int? RemainingHours)> ComputeElapsedRemainingHoursAsync(
        this IBusinessTimeCalculator calculator,
        DateTime now,
        DateTime? start,
        DateTime? due,
        CancellationToken ct = default)
    {
        int? elapsed = start is { } from
            ? (int)Math.Round(await calculator.GetBusinessMinutesBetweenAsync(from, now, ct) / 60.0)
            : null;

        int? remaining = null;
        if (due is { } dueAt)
            // GetBusinessMinutesBetweenAsync returns 0 when from >= to, so the overdue case
            // must swap the arguments and negate to surface the hours past due.
            remaining = now <= dueAt
                ? (int)Math.Round(await calculator.GetBusinessMinutesBetweenAsync(now, dueAt, ct) / 60.0)
                : -(int)Math.Round(await calculator.GetBusinessMinutesBetweenAsync(dueAt, now, ct) / 60.0);

        return (elapsed, remaining);
    }
}
