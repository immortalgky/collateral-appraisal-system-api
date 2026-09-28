namespace Common.Application.Features.Logs;

/// <summary>
/// Bounds a TimeStamp-range query to the clustered-key (Id) range that actually contains those
/// rows, so the main query becomes a contiguous clustered range seek instead of the optimizer's
/// alternative — a backward scan hunting for TOP-N matches, which can't tell when it has passed
/// the window boundary once a residual predicate (e.g. a free-text LIKE) is involved and ends up
/// scanning arbitrarily far past it (measured: a rare term over 24h read the same page count as
/// scanning the whole 30-day retention).
///
/// Id and TimeStamp are NOT strictly co-monotonic: the MSSqlServer sink batches writes every 5s,
/// two app instances flush independently, and clocks skew slightly — so a row with TimeStamp
/// inside [From,To] can still land with Id outside a margin-less [MinId,MaxId]. A safety margin
/// widens the search used to resolve the Id bounds (not the main query's TimeStamp predicate,
/// which stays exact) so a boundary row from an adjacent, slightly-out-of-order batch is never
/// silently excluded — a dropped row in an investigation tool is worse than a few extra reads.
///
/// The MIN/MAX lookup itself is a narrow seek on IX_Logs_TimeStamp (Id is implicitly present as
/// every nonclustered index's row locator, so no bookmark lookups are needed for it).
/// </summary>
public static class LogIdBounding
{
    // Symmetric with MaxIdMarginMinutes: a sink retry after a transient DB blip (SQL Server
    // restart, brief network partition) can re-deliver a row with an in-window TimeStamp but an Id
    // assigned only once the retry lands — which can make IT the earliest-timestamp row in the
    // window, at the wrong (high) end of the Id range, just as easily as it can push the latest-
    // timestamp row's Id too high. An outage shorter than this margin is covered on both sides.
    private const int MinIdMarginMinutes = 60;

    // See MinIdMarginMinutes — same reasoning, same value. Only applies to retrospective windows;
    // see BuildResolveIdBoundsSql for the recent/live case, which skips this bound entirely.
    private const int MaxIdMarginMinutes = 60;

    // How many of the boundary rows (by TimeStamp order) to take Id's MIN/MAX over, instead of
    // trusting a single TOP(1) row. Two app instances can each batch-flush every 5s independently,
    // so several rows right at the edge of the window can be mutually out of Id/TimeStamp order —
    // a single boundary row is not necessarily the one with the most extreme Id nearby. 1000 rows
    // comfortably covers a burst spanning several flush intervals while staying a cheap read (see
    // BuildResolveIdBoundsSql).
    private const int BoundarySampleSize = 1000;

    /// <summary>
    /// Whether bounding is worth applying at all. A residual (non-seekable) predicate always needs
    /// it — see the class doc. Without one, it's still needed for a historical browse: plain
    /// "TOP (@FetchCount) ... ORDER BY Id DESC" with no residual predicate can seek IX_Logs_TimeStamp
    /// for @From, but nothing stops SQL Server from choosing to scan the clustered index backward
    /// from the newest row instead, reading through every row newer than @To before it reaches the
    /// window — cheap for a recent/live window (@To is "now", so there's nothing to scan past), but
    /// not for a browse into last month while a busy day sits between @To and the present. Recent
    /// windows without a residual predicate are the one case that's genuinely cheap either way, so
    /// that's the only one skipped.
    /// </summary>
    public static bool RequiresBounding(DateTime to, DateTime applicationNow, bool hasResidualPredicate) =>
        hasResidualPredicate || !IsRecentWindow(to, applicationNow);

    // Shared by RequiresBounding (should we bound at all?) and BuildResolveIdBoundsSql (does the
    // MaxId half of the bound need its own query, or can it skip straight to bigint.MaxValue?) —
    // same threshold, two different questions, so it's a named helper rather than two copies of the
    // same expression drifting apart.
    private static bool IsRecentWindow(DateTime to, DateTime applicationNow) =>
        to >= applicationNow.AddMinutes(-MaxIdMarginMinutes);

    /// <summary>
    /// Builds the DECLARE/resolve block. The decision of which branch to use is made here in C#,
    /// against the caller's ApplicationNow, not inside the SQL.
    /// </summary>
    /// <param name="skipMaxIdBound">
    /// True for Live's afterId polling, regardless of how old @To is. A stale/cached @To (the
    /// client keeps polling with the same @To from when the page loaded, hours ago) used to still
    /// resolve a real @MaxId bounded near that old @To — which then capped Id BETWEEN @MinId AND
    /// @MaxId below every row that arrived since, so Live silently stopped returning anything once
    /// @To fell more than MaxIdMarginMinutes behind. The main query's own Id > @AfterId is what
    /// actually limits a Live poll; @MaxId doesn't need to.
    /// </param>
    public static string BuildResolveIdBoundsSql(DateTime to, DateTime applicationNow, bool skipMaxIdBound = false)
    {
        // A "TOP (1) ... ORDER BY TimeStamp DESC" only returns ONE row's Id among however many
        // share the latest TimeStamp (ties at the same millisecond, or two instances' batches
        // landing out of order) — for a recent/live window, a genuinely newer row can still be
        // arriving after we've already resolved @MaxId, with a larger Id than whatever we picked.
        // Rather than guess at how much further "recent enough" needs to be, skip the upper Id
        // bound entirely for these windows: the main query's own TimeStamp <= @To (and, for Live's
        // afterId, Id > @AfterId) already limits it correctly.
        var skipMaxIdQuery = skipMaxIdBound || IsRecentWindow(to, applicationNow);

        var maxIdSql = skipMaxIdQuery
            ? "SET @MaxId = 9223372036854775807;" // bigint.MaxValue — no upper bound
            : $"SELECT @MaxId = MAX(Id) FROM (SELECT TOP ({BoundarySampleSize}) Id FROM dbo.Logs WHERE TimeStamp >= @From AND TimeStamp <= DATEADD(MINUTE, {MaxIdMarginMinutes}, @To) ORDER BY TimeStamp DESC, Id DESC) t;";

        // MIN/MAX of a bounded TOP(N) sample, not a single TOP(1) row and not a bare MIN(Id)/MAX(Id)
        // aggregate over the whole TimeStamp range. A single boundary row isn't safe — see
        // BoundarySampleSize — and a bare aggregate over the full range is actively pathological:
        // SQL Server can turn MAX(Id) WHERE TimeStamp-range into a cheap backward scan (recent data
        // is near the end of the clustered key, so it stops almost immediately), but MIN(Id) WHERE
        // TimeStamp-range instead scans FORWARD from the oldest row in the whole table hunting for
        // the first one recent enough — for a narrow/recent window that means reading nearly the
        // entire 30-day retention (measured: 150,455 reads for a 24h window). Bounding both with an
        // inner TOP (...) ORDER BY keeps the seek-then-read-N-rows shape cheap in both directions,
        // and the outer MIN/MAX runs over that small in-memory sample, not the table.
        // ", Id ASC"/", Id DESC" tie-break ties at the same TimeStamp toward the safe (widest) side.
        return $@"
DECLARE @MinId bigint, @MaxId bigint;
SELECT @MinId = MIN(Id) FROM (SELECT TOP ({BoundarySampleSize}) Id FROM dbo.Logs WHERE TimeStamp >= DATEADD(MINUTE, -{MinIdMarginMinutes}, @From) AND TimeStamp <= @To ORDER BY TimeStamp ASC, Id ASC) t;
{maxIdSql}
";
    }

    // Empty window => @MinId is NULL (@MaxId may or may not be, depending on the branch above) =>
    // BETWEEN is false for every row either way, so the main query naturally returns nothing
    // without a separate empty-window branch. The exact TimeStamp predicate stays in the main
    // query (see callers) — this only widens which Ids are considered, it does not by itself
    // decide which rows come back.
    public const string Condition = "Id BETWEEN @MinId AND @MaxId";
}
