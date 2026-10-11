namespace Appraisal.Domain.Appraisals;

/// <summary>
/// The one definition of an open appraisal's SLA status, shared by <see cref="Appraisal.UpdateSlaStatus"/> and the
/// hourly set-based refresh (<c>AppraisalSlaStatusRefreshJob</c>, which interpolates <see cref="AtRiskWindowDays"/>
/// into its SQL), so the two cannot disagree.
/// </summary>
public static class AppraisalSla
{
    /// <summary>
    /// Statuses of a closed appraisal, as a SQL list. Its SLAStatus is left at whatever it closed with, so anything
    /// that reports or refreshes SLA must skip these rows: the refresh job never writes them, and the group counts do
    /// not count them as breached or at risk. One list for both.
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
}
