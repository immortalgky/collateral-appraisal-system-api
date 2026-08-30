using Appraisal.Application.Features.Appraisals.Shared;
using Appraisal.Application.Features.Shared;
using Dapper;
using Shared.CQRS;
using Shared.Data;
using Shared.Identity;

namespace Appraisal.Application.Features.Appraisals.QuickSearch;

/// <summary>
/// The navbar quick-search.
///
/// Every result is an appraisal. The previous implementation returned three independent entity
/// lists, each inventing its own destination — two of which were routes that do not exist, so
/// picking a property result always 404'd. One id, one route, nothing to get wrong.
///
/// Runs as a single batch, following the shape workflow.sp_GetTaskList proved out: materialise the
/// LIKE matches once into a #temp, then read it twice. A CTE referenced twice is re-evaluated, which
/// would run all 17 arms again just to attach the match badges.
///
/// OPTION (RECOMPILE) is load-bearing and measures the opposite way round to the usual intuition:
/// 44 ms with it, 223-241 ms without, and caching the plan does not help. Compiled per execution,
/// the optimizer can see the actual pattern and knows it is a prefix; compiled once for an unknown
/// parameter it has to assume a leading wildcard is possible and picks scans for every arm.
///
/// #m is not indexed. It holds at most ArmCap x arms rows, and building a clustered index on it
/// measured 78 ms against 76 ms without — nothing, on a table that small.
///
/// Not covered: a request that has no appraisal row yet. Every arm joins appraisal.Appraisals, so a
/// draft that has not been turned into an appraisal is invisible here. Measured on the dev database
/// that is 47 of 105,522 non-deleted requests (0.04%), and they are reachable from the request list
/// by the person who raised them — covering them would mean a parallel set of arms and a second
/// result shape for a case the search box is not how anyone finds. The client already renders a
/// null AppraisalNumber as the request number with a badge, so adding it later needs no contract
/// change.
/// </summary>
public class QuickSearchQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser,
    IAddressNameSearch addressNameSearch
) : IQueryHandler<QuickSearchQuery, QuickSearchResult>
{
    /// <summary>
    /// Bounds how long a single keystroke can hold a connection. Nothing in the repo sets a command
    /// timeout, so the default is 30 s — long enough for a debounced burst of typing to pile up
    /// behind one unselective term. A dropdown that has not answered in five seconds is useless
    /// anyway.
    /// </summary>
    private const int CommandTimeoutSeconds = 5;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S2077:Formatting SQL queries is security-sensitive",
        Justification =
            "The only interpolated fragment is AppraisalSearchPredicate's UNION, which is assembled " +
            "from string literals with the search term bound as @SearchPattern. Scope is validated " +
            "against AppraisalSearchPredicate.Scopes before it reaches the builder and never appears " +
            "in the command text.")]
    public async Task<QuickSearchResult> Handle(QuickSearchQuery query, CancellationToken cancellationToken)
    {
        // Capped: this runs on every keystroke and shows a handful of rows. Callers that present a
        // complete result set leave the cap off — see AppraisalSearchPredicate.DropdownArmCap.
        var addressMatch = await addressNameSearch.MatchAsync(query.Q, cancellationToken);
        var built = AppraisalSearchPredicate.Build(
            query.Q, query.Scope, AppraisalSearchPredicate.DropdownArmCap, addressMatch);
        if (built is null) return Empty;

        var (armsSql, parameters) = built.Value;
        var limit = Math.Clamp(query.Limit, 1, 20);
        parameters.Add("Limit", limit);

        // External valuation companies see only their own assignments. Applied as a semi-join on
        // the temp table rather than a join into the view, so scoping costs one seek per candidate
        // instead of resolving the latest assignment for the whole match set.
        var enforcedCompanyId = AppraisalAccessScope.GetEnforcedCompanyId(currentUser);
        var scopeFilter = "";
        if (enforcedCompanyId.HasValue)
        {
            // AssigneeCompanyId is nvarchar(100): binding a Guid makes SQL Server TRY_CAST every
            // row's value and throw on the ones holding non-GUID text.
            parameters.Add("ScopedCompanyId", enforcedCompanyId.Value.ToString());
            scopeFilter = """
                DELETE m FROM #m m
                WHERE NOT EXISTS (SELECT 1 FROM appraisal.vw_AppraisalList v
                                  WHERE v.Id = m.AppraisalId AND v.AssigneeCompanyId = @ScopedCompanyId);
                """;
        }

        var sql = $"""
            SELECT AppraisalId, Rnk, Fld, Val INTO #m FROM (
            {armsSql}
            ) x OPTION (RECOMPILE, MAXDOP 1);

            {scopeFilter}

            SELECT TOP(@Limit) AppraisalId, MIN(Rnk) AS Rnk
            INTO #t FROM #m GROUP BY AppraisalId ORDER BY MIN(Rnk), AppraisalId;

            SELECT COUNT(DISTINCT AppraisalId) FROM #m;

            SELECT v.Id AS AppraisalId, v.AppraisalNumber, v.RequestId, v.RequestNumber,
                   v.CustomerName, v.Status, v.PropertyTypes, v.Province, t.Rnk
            FROM #t t
            JOIN appraisal.vw_AppraisalList v ON v.Id = t.AppraisalId
            ORDER BY t.Rnk, v.AppraisalNumber
            OPTION (RECOMPILE, MAXDOP 1);

            SELECT m.AppraisalId, m.Rnk, m.Fld, m.Val
            FROM #m m JOIN #t t ON t.AppraisalId = m.AppraisalId
            ORDER BY m.Rnk;

            DROP TABLE #m; DROP TABLE #t;
            """;

        // GetOpenConnection returns the connection shared by the whole request scope — it must not
        // be disposed here. The endpoint this replaced wrapped it in `using`, which closed it out
        // from under every later Dapper call in the same request.
        var connection = connectionFactory.GetOpenConnection();
        await using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            sql, parameters, commandTimeout: CommandTimeoutSeconds, cancellationToken: cancellationToken));

        var matched = await grid.ReadSingleAsync<int>();
        var heads = (await grid.ReadAsync<HeadRow>()).ToList();
        var matches = (await grid.ReadAsync<MatchRow>()).ToList();

        if (heads.Count == 0) return Empty;

        // `matched` counts distinct appraisals in the capped #m, so it is a floor, not a total: an
        // arm that hit its cap contributed at most DropdownArmCap rows, and #m holds one row per
        // MATCH, so an appraisal matching on five titles consumes five of them. Reporting it as a
        // total would under-count exactly the broad terms where the number matters. The client is
        // told it is approximate and shows "N+" instead.
        var capped = matches.Count >= AppraisalSearchPredicate.DropdownArmCap;

        return new QuickSearchResult(
            BuildGroups(heads, matches),
            HasMore: matched > heads.Count,
            TotalMatchedAppraisals: matched,
            IsTotalApproximate: capped);
    }

    private static readonly QuickSearchResult Empty = new([], false, 0);

    /// <summary>
    /// Groups the flat rows by the value that matched.
    ///
    /// An appraisal can match several ways at once (a title deed and its owner, say). It is placed
    /// in the group of its own best match so it appears exactly once, while every match it has still
    /// rides along in <see cref="SearchAppraisal.MatchedOn"/> for the badges.
    /// </summary>
    private static List<SearchGroup> BuildGroups(List<HeadRow> heads, List<MatchRow> matches)
    {
        var byAppraisal = matches
            .GroupBy(m => m.AppraisalId)
            .ToDictionary(g => g.Key, g => g.OrderBy(m => m.Rnk).ToList());

        var groups = new List<SearchGroup>();
        var index = new Dictionary<(string Field, string Label), List<SearchAppraisal>>();

        foreach (var head in heads)
        {
            var rows = byAppraisal.TryGetValue(head.AppraisalId, out var m) ? m : [];
            var best = rows.FirstOrDefault();
            if (best is null) continue;

            var item = new SearchAppraisal(
                head.AppraisalId,
                head.AppraisalNumber,
                head.RequestId,
                head.RequestNumber,
                head.CustomerName,
                head.Status,
                head.PropertyTypes,
                head.Province,
                $"/appraisals/{head.AppraisalId}",
                rows.Select(r => new SearchMatch(r.Fld, r.Val ?? "")).ToList());

            var key = (best.Fld, best.Val ?? "");
            if (!index.TryGetValue(key, out var bucket))
            {
                bucket = [];
                index[key] = bucket;
                groups.Add(new SearchGroup(KindOf(best.Rnk), key.Item2, best.Fld, 0, bucket));
            }

            bucket.Add(item);
        }

        // AppraisalCount is fixed up now that every bucket is filled; records are immutable, and the
        // count is what the group header shows.
        return groups.Select(g => g with { AppraisalCount = g.Appraisals.Count }).ToList();
    }

    /// <summary>Maps the arm's rank band back to the group icon the client draws.</summary>
    private static string KindOf(int rank) => rank switch
    {
        < 20 => "document",
        < 30 => "customer",
        _ => "property"
    };

    private sealed class HeadRow
    {
        public Guid AppraisalId { get; set; }
        public string? AppraisalNumber { get; set; }
        public Guid RequestId { get; set; }
        public string? RequestNumber { get; set; }
        public string? CustomerName { get; set; }
        public string? Status { get; set; }
        public string? PropertyTypes { get; set; }
        public string? Province { get; set; }
        public int Rnk { get; set; }
    }

    private sealed class MatchRow
    {
        public Guid AppraisalId { get; set; }
        public int Rnk { get; set; }
        public string Fld { get; set; } = "";
        public string? Val { get; set; }
    }
}
