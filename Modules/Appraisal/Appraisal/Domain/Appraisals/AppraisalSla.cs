namespace Appraisal.Domain.Appraisals;

/// <summary>
/// The one definition of an open appraisal's SLA status, shared by <see cref="Appraisal.UpdateSlaStatus"/> and the
/// hourly set-based refresh (<c>AppraisalSlaStatusRefreshJob</c>, which interpolates <see cref="AtRiskWindowDays"/>
/// into its SQL), so the two cannot disagree.
/// </summary>
public static class AppraisalSla
{
    /// <summary>
    /// Statuses of a closed appraisal, as a SQL list. Its SLAStatus is fixed once, when it closes
    /// (<see cref="ClosedStatusAt"/>), and the hourly refresh never writes it again; the group counts also skip these
    /// rows, since a delivered job is not "needs attention". One list for both.
    /// </summary>
    public const string ClosedStatusesSql = "('Completed', 'Cancelled')";

    /// <summary>An appraisal due within this many days is AtRisk.</summary>
    public const int AtRiskWindowDays = 2;

    /// <summary>
    /// Breached at or after the due date, AtRisk within <see cref="AtRiskWindowDays"/> days of it, else OnTrack.
    /// Compared as instants, not whole days: <c>(due - now).Days</c> truncates toward zero, so something 12 hours
    /// overdue used to read 0 days and come out AtRisk instead of Breached.
    /// </summary>
    public static string StatusAt(DateTime dueDate, DateTime now) =>
        dueDate <= now ? "Breached"
        : dueDate <= now.AddDays(AtRiskWindowDays) ? "AtRisk"
        : "OnTrack";

    /// <summary>
    /// The final status of an appraisal that closes (completes or is cancelled) at <paramref name="closedAt"/>:
    /// Breached if it closed after the due date, else OnTrack. Never AtRisk — a closed job has no risk left. Same
    /// comparison as <c>IsWithinSLA</c> (closing exactly on the due date is on time). The one-time data script
    /// that settled already-closed rows applies this rule in SQL.
    /// </summary>
    public static string ClosedStatusAt(DateTime dueDate, DateTime closedAt) =>
        closedAt > dueDate ? "Breached" : "OnTrack";
}
