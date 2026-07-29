namespace Appraisal.Application.Services;

/// <summary>
/// Resolves an appraisal's APPRAISAL DATE from its appointments — the single derivation rule behind
/// ValuationAnalyses.ValuationDate.
///
/// <para>
/// ValuationDate now LEADS every read surface that shows an appraisal date: the printed book, both
/// AS400 result feeds, vw_AppraisalDetail, decision summary, History Search, and the +5-year
/// reappraisal anchor in vw_ReappraisalCandidates / vw_RCAS002_ReappraisalDue. That makes it a
/// reported date, not an internal timestamp — so every writer has to derive it the same way, and
/// none of them may quietly substitute "now" for a date they could not resolve.
/// </para>
///
/// <para>
/// Cancelled appointments are excluded to match the read side, which filters
/// <c>Status &lt;&gt; 'Cancelled'</c> everywhere. Returning null (rather than a fallback) is
/// deliberate: only the caller knows whether it has a stored value worth preserving, and that
/// choice must not be buried here.
/// </para>
/// </summary>
public class AppraisalDateResolver(AppraisalDbContext db)
{
    /// <summary>
    /// The latest non-cancelled appointment date across all of the appraisal's assignments, or null
    /// when none exists — no assignments, no appointments, or every appointment cancelled.
    /// </summary>
    public async Task<DateTime?> ResolveFromAppointmentsAsync(Guid appraisalId, CancellationToken ct)
    {
        var assignmentIds = await db.AppraisalAssignments
            .Where(a => a.AppraisalId == appraisalId)
            .Select(a => a.Id)
            .ToListAsync(ct);

        if (assignmentIds.Count == 0)
            return null;

        // Status is a plain string on Appointment, so this predicate translates server-side.
        var dates = await db.Appointments
            .Where(ap => assignmentIds.Contains(ap.AssignmentId) && ap.Status != "Cancelled")
            .Select(ap => ap.AppointmentDateTime)
            .ToListAsync(ct);

        return dates.Count > 0 ? dates.Max() : null;
    }
}
