using Appraisal.Contracts.Appraisals;
using Dapper;

namespace Appraisal.Application.Features.Appraisals.ResolveLatestInAppraisalChain;

/// <summary>
/// Resolves an appraisal chain purely from <c>appraisal.Appraisals.PrevAppraisalId</c>.
///
/// Two recursive walks rather than one: UP from the picked appraisal to the chain root, then DOWN
/// over every descendant of that root. The down-walk is what lets a user pick the ORIGINAL appraisal
/// and still resolve to the newest inspection — walking ancestors alone would report such a chain as
/// having no inspections at all.
///
/// Both walks guard cycles with the Path/CHARINDEX test and lift the recursion cap with
/// <c>OPTION (MAXRECURSION 0)</c>, copied from <c>GetAppraisalForCollateralQueryHandler</c>.
/// Do NOT swap either for a depth predicate: truncation would be silent, and the caller would take
/// the Nth ancestor for the chain root.
///
/// The two CTEs run through <c>dbContext.Database</c>, NOT <see cref="ISqlConnectionFactory"/>, so
/// they share the context's connection and enlist in whatever transaction it is in. Today both
/// callers happen to run outside one (<c>AppraisalCreationService</c> only opens its transaction
/// later, at the first SaveChanges), but a caller that did hold locks on appraisal.Appraisals — the
/// very table these read — would deadlock against a second connection until it timed out. Keeping
/// the read on the context's own connection makes that impossible by construction. The company-name
/// lookup below is the exception: auth.Companies is a different table, so its own connection is safe.
/// </summary>
public class ResolveLatestInAppraisalChainQueryHandler(
    AppraisalDbContext dbContext,
    ISqlConnectionFactory connectionFactory
) : IRequestHandler<ResolveLatestInAppraisalChainQuery, AppraisalChainRef?>
{
    public async Task<AppraisalChainRef?> Handle(
        ResolveLatestInAppraisalChainQuery query,
        CancellationToken cancellationToken)
    {
        // Walk UP to the chain root. TOP 1 by descending depth is the last node reached. Returns
        // the picked id itself for a single-node chain.
        //
        // Only the ANCHOR filters IsDeleted (a deleted pick must resolve to null). The recursive
        // member deliberately walks THROUGH soft-deleted rows: filtering them in the JOIN would prune
        // the entire subtree beyond them, so one deleted appraisal mid-chain would hide every later
        // inspection and reset the round number to 1. Deleted rows are excluded from the results
        // instead — the Appraisal aggregate carries a soft-delete global query filter, so the LINQ
        // below never sees them.
        const string rootSql = """
            WITH up AS (
                SELECT a.Id, a.PrevAppraisalId, 1 AS Depth,
                       CAST('|' + CAST(a.Id AS varchar(36)) + '|' AS varchar(max)) AS Path
                FROM appraisal.Appraisals a
                WHERE a.Id = {0} AND a.IsDeleted = 0

                UNION ALL

                SELECT p.Id, p.PrevAppraisalId, u.Depth + 1,
                       CAST(u.Path + CAST(p.Id AS varchar(36)) + '|' AS varchar(max))
                FROM up u
                JOIN appraisal.Appraisals p ON p.Id = u.PrevAppraisalId
                WHERE CHARINDEX('|' + CAST(p.Id AS varchar(36)) + '|', u.Path) = 0
            )
            SELECT TOP 1 u.Id AS [Value]
            FROM up u
            ORDER BY u.Depth DESC
            OPTION (MAXRECURSION 0)
            """;

        var rootId = await dbContext.Database
            .SqlQueryRaw<Guid>(rootSql, query.PickedPrevAppraisalId)
            .ToListAsync(cancellationToken);

        // Empty means the picked appraisal does not exist or is soft-deleted.
        if (rootId.Count == 0)
            return null;

        // Walk DOWN over the whole tree. A chain can fork (two requests copying the same prior), so
        // this is a tree, not a list — the caller picks the latest completed node out of it.
        // No IsDeleted filter at all here: the root itself may be a soft-deleted ancestor, and the
        // global query filter removes deleted rows from the results either way.
        const string chainSql = """
            WITH down AS (
                SELECT a.Id,
                       CAST('|' + CAST(a.Id AS varchar(36)) + '|' AS varchar(max)) AS Path
                FROM appraisal.Appraisals a
                WHERE a.Id = {0}

                UNION ALL

                SELECT c.Id,
                       CAST(d.Path + CAST(c.Id AS varchar(36)) + '|' AS varchar(max))
                FROM down d
                JOIN appraisal.Appraisals c ON c.PrevAppraisalId = d.Id
                WHERE CHARINDEX('|' + CAST(c.Id AS varchar(36)) + '|', d.Path) = 0
            )
            SELECT d.Id AS [Value]
            FROM down d
            OPTION (MAXRECURSION 0)
            """;

        var chainIds = await dbContext.Database
            .SqlQueryRaw<Guid>(chainSql, rootId[0])
            .ToListAsync(cancellationToken);

        // Hoisted so EF parameterises the codes rather than trying to translate the static
        // AppraisalStatus factories inside the expression tree.
        var completedStatus = AppraisalStatus.Completed.Code;
        var cancelledStatus = AppraisalStatus.Cancelled.Code;

        // Count every inspection the chain already holds. Cancelled ones are excluded so an
        // abandoned request does not inflate the round number; in-flight ones ARE counted, which the
        // engagement-based predecessor could not do (engagement rows only appear after completion).
        var progressiveCount = await dbContext.Appraisals
            .AsNoTracking()
            .CountAsync(
                a => chainIds.Contains(a.Id)
                     && a.AppraisalType == AppraisalTypes.Progressive
                     && a.Status.Code != cancelledStatus,
                cancellationToken);

        // The copy/company/fee source: the most recently completed appraisal anywhere in the tree.
        // Ordered by CompletedAt — the only completion clock on the aggregate itself; the valuation
        // date lives in appraisal.ValuationAnalyses and would cost a join for no practical gain,
        // since inspections complete in the same order they are valued.
        var latest = await dbContext.Appraisals
            .AsNoTracking()
            .Where(a => chainIds.Contains(a.Id) && a.Status.Code == completedStatus)
            .OrderByDescending(a => a.CompletedAt)
            .ThenByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Select(a => a.Id)
            .FirstOrDefaultAsync(cancellationToken);

        // Nothing completed in the chain: hand back the picked appraisal so the caller still copies
        // from what the user chose. PriorAppraisalSubmissionGuard already rejects that case at
        // submit, so this is defence in depth rather than an expected path.
        var resolvedId = latest == Guid.Empty ? query.PickedPrevAppraisalId : latest;

        var (companyId, companyName) = await ResolveCompanyAsync(resolvedId, cancellationToken);

        return new AppraisalChainRef(resolvedId, companyId, companyName, progressiveCount);
    }

    /// <summary>
    /// The external company that performed an appraisal, taken from its latest live assignment —
    /// election and null-handling both mirror <c>GetAppraisalForCollateralQueryHandler</c>, the code
    /// that stamped this value onto the engagement, so this reproduces the old lookup's answer.
    /// </summary>
    private async Task<(Guid? CompanyId, string CompanyName)> ResolveCompanyAsync(
        Guid appraisalId,
        CancellationToken cancellationToken)
    {
        // Elect the newest assignment FIRST, then read whatever company it carries — never pick the
        // newest assignment that happens to have one. A case re-assigned from an external company to
        // an internal appraiser leaves a company-less latest assignment, and that means "no external
        // company performed this", not "fall back to the one before"; skipping it would hard-route
        // the next inspection to a company that did not do the prior work. This is the same election
        // GetAppraisalForCollateralQueryHandler used to stamp the value onto the engagement.
        //
        // Rejected and Cancelled assignments never arrive here — AppraisalAssignment carries a global
        // query filter for them, which is also why no status predicate appears below (AssignmentStatus
        // is value-converted, so `.Code` would not translate anyway).
        var rawCompanyId = await dbContext.AppraisalAssignments
            .AsNoTracking()
            .Where(a => a.AppraisalId == appraisalId)
            .OrderByDescending(a => a.AssignedAt)
            .ThenByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Select(a => a.AssigneeCompanyId)
            .FirstOrDefaultAsync(cancellationToken);

        // Internal work carries no company — there is nothing to force or exclude downstream.
        if (!Guid.TryParse(rawCompanyId, out var companyId))
            return (null, string.Empty);

        // auth.Companies is outside this DbContext, and the name must come from there rather than
        // AppraisalAssignment.ExternalAppraiserName — that column holds a person, not a company.
        const string nameSql = """
            SELECT TOP 1 c.Name
            FROM auth.Companies c
            WHERE c.Id = @CompanyId AND c.IsDeleted = 0
            """;

        var connection = connectionFactory.GetOpenConnection();
        var name = await connection.QueryFirstOrDefaultAsync<string?>(
            nameSql, new { CompanyId = companyId });

        return (companyId, name ?? string.Empty);
    }
}
