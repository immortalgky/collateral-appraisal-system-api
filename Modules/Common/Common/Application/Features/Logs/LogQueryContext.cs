using Dapper;
using Shared.Exceptions;

namespace Common.Application.Features.Logs;

/// <summary>
/// Shared plumbing for SearchLogs and GetLogSummary: validates `q`, resolves the time range, parses
/// the free-text/key:value query into a WHERE clause, and decides whether Id bounding (see
/// LogIdBounding) is worth applying. Built once so the two handlers can't drift on any of this — a
/// past review round caught them disagreeing on exactly when to apply bounding.
/// </summary>
public sealed record LogQueryContext(
    DateTime From,
    DateTime To,
    LogQueryParser.Result Parsed,
    List<string> Conditions,
    DynamicParameters Parameters,
    string IdBoundsSql)
{
    private const int MaxQueryLength = 500;

    /// <param name="includeUpperTimeBound">
    /// False for Live's afterId polling: with clock skew across app instances, a row stamped
    /// slightly "in the future" relative to @To could otherwise be excluded by TimeStamp &lt;= @To
    /// forever, since the cursor only ever moves past its Id. The lower @From bound still applies —
    /// only the upper one is caller-controlled. Also drives skipping the MaxId upper bound (see
    /// LogIdBounding.BuildResolveIdBoundsSql's skipMaxIdBound) — the same afterId scenario, same fix.
    /// </param>
    /// <param name="forceBounding">
    /// True when the caller has its own non-seekable-in-Id-order filter that LogQueryParser doesn't
    /// know about — SearchLogs' `levels` filter, or sortDir=asc (a forward TOP-N scan starts from
    /// the oldest retained row in the whole 30-day table, not from @From, unless bounded — same
    /// pathology as the old margin-less MIN(Id) lookup, just in the main query this time). Measured:
    /// TOP(N) ... ORDER BY Id DESC with just "Level IN ('Error')" over a recent 24h window (no
    /// free-text q, so HasResidualPredicate is false) made the optimizer pick a Clustered Index Scan
    /// ORDERED BACKWARD filtering Level + TimeStamp row-by-row — 155,518 logical reads — instead of
    /// the cheap IX_Logs_Level_TimeStamp seek it uses when Id isn't also being sorted on.
    /// </param>
    public static LogQueryContext Build(
        string? q, DateTime? from, DateTime? to, DateTime applicationNow,
        bool includeUpperTimeBound = true, bool forceBounding = false)
    {
        if (q?.Length > MaxQueryLength)
            throw new BadRequestException($"'q' must not exceed {MaxQueryLength} characters.");

        var (resolvedFrom, resolvedTo) = LogFilterRange.Resolve(from, to, applicationNow);
        var parsed = LogQueryParser.Parse(q);

        var conditions = new List<string> { "TimeStamp >= @From" };
        if (includeUpperTimeBound)
            conditions.Add("TimeStamp <= @To");

        var parameters = parsed.Parameters;
        parameters.Add("From", resolvedFrom);
        parameters.Add("To", resolvedTo);

        if (parsed.WhereClause is not null)
            conditions.Add(parsed.WhereClause);

        // HasLevelKey (a `level:` term inside q) needs bounding for the same reason as SearchLogs'
        // `levels` filter — see forceBounding's doc comment. corr:/request:/user:/appraisal:/a bare
        // GUID don't: each seeks its own selective single-column index regardless of Id ordering
        // (measured single-digit logical reads), so LogQueryParser doesn't fold them into this.
        var applyBounding = forceBounding || parsed.HasLevelKey
            || LogIdBounding.RequiresBounding(resolvedTo, applicationNow, parsed.HasResidualPredicate);
        if (applyBounding)
            conditions.Add(LogIdBounding.Condition);

        var idBoundsSql = applyBounding
            ? LogIdBounding.BuildResolveIdBoundsSql(resolvedTo, applicationNow, skipMaxIdBound: !includeUpperTimeBound)
            : "";

        return new LogQueryContext(resolvedFrom, resolvedTo, parsed, conditions, parameters, idBoundsSql);
    }
}
