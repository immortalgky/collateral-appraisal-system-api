namespace Appraisal.Application.Features.Appraisals.ResolveLatestInAppraisalChain;

/// <summary>
/// The whole tree an appraisal belongs to, from <c>appraisal.Appraisals.PrevAppraisalId</c>: UP from the
/// picked appraisal to the chain root, then DOWN over every descendant. Shared by
/// <see cref="ResolveLatestInAppraisalChainQueryHandler"/> and the construction chain status query so both
/// see the same tree. See the first handler's remarks for why the recursion is uncapped and why the walk
/// runs on the context's own connection.
/// </summary>
internal static class AppraisalChainTree
{
    /// <summary>Every appraisal id in the picked appraisal's tree, or null when it does not exist or is soft-deleted.</summary>
    public static async Task<List<Guid>?> LoadAsync(
        AppraisalDbContext db,
        Guid pickedAppraisalId,
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

        var rootId = await db.Database
            .SqlQueryRaw<Guid>(rootSql, pickedAppraisalId)
            .ToListAsync(cancellationToken);

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

        var chainIds = await db.Database
            .SqlQueryRaw<Guid>(chainSql, rootId[0])
            .ToListAsync(cancellationToken);

        return chainIds;
    }
}
