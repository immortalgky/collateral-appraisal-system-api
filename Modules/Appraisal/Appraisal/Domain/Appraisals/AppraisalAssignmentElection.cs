namespace Appraisal.Domain.Appraisals;

/// <summary>
/// THE rule for "which assignment row is the appraisal's current one": the newest live (not Rejected / Cancelled)
/// row by creation. Used wherever the admin stage (fee edits, preview) and the consumers that assign and bill pick
/// a row, so an admin's edit and the fee that assignment materialises always land on the same row. Creation order,
/// not AssignedAt: a row awaiting assignment has no AssignedAt and would otherwise lose to an older assigned one.
/// </summary>
public static class AppraisalAssignmentElection
{
    /// <summary>
    /// Query form. The global query filter already drops Rejected and Cancelled rows, which is why no status
    /// predicate appears (AssignmentStatus is value-converted, so `.Code` would not translate anyway).
    /// </summary>
    public static IOrderedQueryable<AppraisalAssignment> NewestFirst(this IQueryable<AppraisalAssignment> assignments) =>
        assignments.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id);

    /// <summary>The newest live assignment of one appraisal first: the single query every caller electing "the current row" starts from.</summary>
    public static IOrderedQueryable<AppraisalAssignment> NewestFirstFor(this IQueryable<AppraisalAssignment> assignments, Guid appraisalId) =>
        assignments.Where(a => a.AppraisalId == appraisalId).NewestFirst();

    /// <summary>In-memory form, over an aggregate's loaded assignments (the filter does not apply there).</summary>
    public static AppraisalAssignment? Current(this IEnumerable<AppraisalAssignment> assignments) =>
        assignments
            .Where(a => a.AssignmentStatus != AssignmentStatus.Rejected && a.AssignmentStatus != AssignmentStatus.Cancelled)
            .OrderByDescending(a => a.CreatedAt)
            // SQL Server orders uniqueidentifier by its trailing bytes first, .NET by the leading ones: tie-break the
            // way the query form does, or the two forms pick different rows for equal CreatedAt.
            .ThenByDescending(a => new System.Data.SqlTypes.SqlGuid(a.Id))
            .FirstOrDefault();
}
