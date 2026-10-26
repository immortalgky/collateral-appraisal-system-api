namespace Appraisal.Application.Features.Appraisals.ResolveLatestInAppraisalChain;

/// <summary>
/// Which external company performed an appraisal: the company of its newest live assignment. The ONE implementation
/// of that election, shared by the chain resolution (which adds the company name) and the assignment defaults.
/// </summary>
internal static class AppraisalCompanyElection
{
    /// <summary>
    /// Elect the newest assignment FIRST, then read whatever company it carries — never pick the newest assignment
    /// that happens to have one. A case re-assigned from an external company to an internal appraiser leaves a
    /// company-less latest assignment, and that means "no external company performed this", not "fall back to the
    /// one before"; skipping it would hard-route the next inspection to a company that did not do the prior work.
    /// This is the same election GetAppraisalForCollateralQueryHandler used to stamp the value onto the engagement.
    ///
    /// Rejected and Cancelled assignments never arrive here — AppraisalAssignment carries a global query filter for
    /// them, which is also why no status predicate appears below (AssignmentStatus is value-converted, so `.Code`
    /// would not translate anyway). Null for internal work: nothing to force or exclude downstream.
    /// </summary>
    public static async Task<Guid?> ElectCompanyIdAsync(
        AppraisalDbContext dbContext, Guid appraisalId, CancellationToken cancellationToken) =>
        Guid.TryParse(await RawCompanyId(dbContext, appraisalId).FirstOrDefaultAsync(cancellationToken), out var companyId)
            ? companyId
            : null;

    /// <summary>
    /// The election as a query (at most one row: the newest live assignment's raw company id, null when it has
    /// none), so a caller that is already reading the appraisal can fold it into that projection instead of paying a
    /// round trip. <see cref="ElectCompanyIdAsync"/> is this plus the parse.
    /// </summary>
    public static IQueryable<string?> RawCompanyId(AppraisalDbContext dbContext, Guid appraisalId) =>
        dbContext.AppraisalAssignments
            .AsNoTracking()
            .NewestFirstFor(appraisalId)
            .Select(a => a.AssigneeCompanyId)
            .Take(1);
}
