using Dapper;
using Shared.CQRS;
using Shared.Data;
using Shared.Time;

namespace Common.Application.Features.Logs.SearchLogs;

public class SearchLogsQueryHandler(ISqlConnectionFactory connectionFactory, IDateTimeProvider dateTimeProvider)
    : IQueryHandler<SearchLogsQuery, SearchLogsResult>
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;

    private static readonly string[] KnownLevels = ["Information", "Warning", "Error", "Fatal"];

    public async Task<SearchLogsResult> Handle(SearchLogsQuery query, CancellationToken cancellationToken)
    {
        var filter = query.Filter;
        var applicationNow = dateTimeProvider.ApplicationNow;
        var pageSize = Math.Clamp(filter.PageSize ?? DefaultPageSize, 1, MaxPageSize);
        var sortDir = string.Equals(filter.SortDir, "asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";

        // Every value in `levels` unknown (e.g. a typo, or a stale FE build sending an old name) —
        // return an empty page rather than silently dropping the filter and showing every level.
        if (!TryParseLevels(filter.Levels, out var levels))
            return new SearchLogsResult([], false);

        // Live (afterId) never applies the upper TimeStamp bound — see LogQueryContext.Build's doc
        // comment on includeUpperTimeBound. forceBounding whenever a levels filter is set (see that
        // param's doc comment) or the requested order is ascending: an unanchored "ORDER BY Id ASC"
        // browse (no afterId/beforeId already seeking to a specific Id) starts its scan from the
        // oldest retained row in the whole 30-day table, not from @From, unless bounded.
        var ctx = LogQueryContext.Build(filter.Q, filter.From, filter.To, applicationNow,
            includeUpperTimeBound: !filter.AfterId.HasValue,
            forceBounding: levels.Length > 0 || sortDir == "ASC");
        var conditions = ctx.Conditions;
        var parameters = ctx.Parameters;

        if (levels.Length > 0)
        {
            conditions.Add("Level IN @Levels");
            parameters.Add("Levels", levels);
        }

        // Live mode always fetches ascending (oldest-of-the-new-rows first) starting right after
        // the cursor, regardless of the requested sortDir. Fetching newest-first instead (TOP N
        // ORDER BY Id DESC) would silently skip the middle of a burst: once more than pageSize rows
        // arrived since the last poll, DESC + TOP only returns the newest slice, the client advances
        // its cursor to the top of that slice, and the rows between the old cursor and the returned
        // slice are never seen again. Fetching ascending never skips — hasMore then means "more
        // newer rows exist beyond this batch", so the FE should re-poll immediately (not wait for the
        // next 5s tick) until hasMore is false. Reversed to newest-first below to match display order.
        // Accepted limitation: Id is a bigint IDENTITY, and the identity value is reserved before
        // the row commits — two concurrent batches can commit out of Id order, so a rare reorder
        // within one poll interval is possible (a lower Id committing after a higher one already
        // returned). The row isn't lost, just late by one poll; the next normal (non-Live) search
        // over the same window shows it correctly ordered.
        var effectiveSortDir = sortDir;
        if (filter.AfterId.HasValue)
        {
            conditions.Add("Id > @AfterId");
            parameters.Add("AfterId", filter.AfterId.Value);
            effectiveSortDir = "ASC";
        }
        else if (filter.BeforeId.HasValue)
        {
            // "Load more" in the current sort direction — older-in-order rows than the last one seen.
            conditions.Add(sortDir == "ASC" ? "Id > @BeforeId" : "Id < @BeforeId");
            parameters.Add("BeforeId", filter.BeforeId.Value);
        }

        var sql = $@"
{ctx.IdBoundsSql}
SELECT TOP (@FetchCount)
    Id, TimeStamp, Level, Message, Exception, CorrelationId, EntityId, AppraisalId, RequestId,
    WorkflowInstanceId, CollateralId, DocumentId, MachineName, UserName,
    COALESCE(SourceContext, JSON_VALUE(Properties, '$.Properties.SourceContext')) AS SourceContext,
    COALESCE(RequestPath, JSON_VALUE(Properties, '$.Properties.RequestPath')) AS RequestPath
FROM dbo.Logs
WHERE {string.Join(" AND ", conditions)}
ORDER BY Id {effectiveSortDir}";

        // Fetch one extra row to know whether there's more without a separate COUNT query.
        parameters.Add("FetchCount", pageSize + 1);

        var connection = connectionFactory.GetOpenConnection();
        var rows = (await connection.QueryAsync<LogListItem>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))).ToList();

        var hasMore = rows.Count > pageSize;
        if (hasMore) rows.RemoveAt(rows.Count - 1);
        // Only reverse back to newest-first when that's the requested order — Live almost always
        // asks for desc, but if a caller combines afterId with an explicit sortDir=asc, the
        // ascending fetch already matches what was requested.
        if (filter.AfterId.HasValue && sortDir == "DESC") rows.Reverse();

        return new SearchLogsResult(rows, hasMore);
    }

    // Keeps only the 4 known levels (case-insensitive) and dedupes — a caller sending a huge garbage
    // CSV in `levels` used to turn into "Level IN @Levels" with thousands of parameters, which SQL
    // Server's ~2100 parameter limit turns into a 500. At most 4 distinct values now.
    //
    // Returns false only when `levels` was actually supplied but none of its values matched a known
    // level — the caller treats that as "return nothing", not "no filter". True + an empty array
    // means no filter was requested at all (levels was null/blank), which is different: that case
    // means "show every level", not "show none".
    private static bool TryParseLevels(string? levels, out string[] known)
    {
        known = [];
        if (string.IsNullOrWhiteSpace(levels)) return true;

        var rawTokens = levels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (rawTokens.Length == 0) return true;

        known = rawTokens
            .Select(l => KnownLevels.FirstOrDefault(k => string.Equals(k, l, StringComparison.OrdinalIgnoreCase)))
            .OfType<string>()
            .Distinct()
            .ToArray();

        return known.Length > 0;
    }
}
