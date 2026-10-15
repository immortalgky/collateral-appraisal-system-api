using Collateral.CollateralMasters.Reappraisal;
using Dapper;

namespace Collateral.Application.Features.Reappraisal.GetCandidates;

/// <summary>
/// Read-side handler for the Reappraisal Candidates list page.
/// Uses Dapper + DynamicParameters against collateral.vw_ReappraisalCandidates, one row per book.
/// Lists one status tab at a time: Pending and Deleted only for books on AS400's latest file (a book
/// that has dropped off the file is no longer due); Consumed — the processed history — all of them.
/// </summary>
public class GetReappraisalCandidatesQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetReappraisalCandidatesQuery, GetReappraisalCandidatesResult>
{
    public async Task<GetReappraisalCandidatesResult> Handle(
        GetReappraisalCandidatesQuery query,
        CancellationToken cancellationToken)
    {
        var consumed = string.Equals(query.Status, nameof(ReappraisalCandidateStatus.Consumed), StringComparison.OrdinalIgnoreCase);
        var sql = consumed ? ConsumedSql : """
            SELECT
                c.Id,
                c.Status,
                c.ReviewType,
                c.AppraisalDate,
                c.DueDate,
                c.RemainingDay,
                c.OldAppraisalReportNumber,
                c.CifNumber,
                c.CustomerName,
                c.CollateralId,
                c.CollateralName,
                c.CurrentValue,
                c.HasOpenAppraisal,
                c.OpenAppraisalId,
                c.OpenAppraisalNumber,
                c.OpenAppraisalGroupTag,
                c.OpenRequestId,
                c.OpenRequestNumber,
                c.PriorAppraisalSource,
                c.FirstSeenFileDate,
                c.LastSeenFileDate,
                c.IsBlockUnit,
                un.MatchedUnits AS UnitMatchedUnits,
                un.TowerName    AS UnitTowerName,
                un.Floor        AS UnitFloor,
                un.RoomNumber   AS UnitRoomNumber,
                un.HouseNumber  AS UnitHouseNumber,
                un.PlotNumber   AS UnitPlotNumber
            FROM collateral.vw_ReappraisalCandidates c
            OUTER APPLY (
                -- The project unit a block-project row matched (collateral.vw_ReappraisalCandidateUnits).
                SELECT TOP 1 cu.MatchedUnits, u.TowerName, u.Floor, u.RoomNumber, u.HouseNumber, u.PlotNumber
                FROM collateral.vw_ReappraisalCandidateUnits cu
                LEFT JOIN appraisal.ProjectUnits u ON u.Id = cu.ProjectUnitId
                WHERE cu.CandidateId = c.Id
            ) un
            WHERE c.Status = @Status
              AND c.IsInLatestFile = 1
            """;

        var p = new DynamicParameters();
        p.Add("Status", string.Equals(query.Status, nameof(ReappraisalCandidateStatus.Deleted), StringComparison.OrdinalIgnoreCase)
            ? nameof(ReappraisalCandidateStatus.Deleted)
            : nameof(ReappraisalCandidateStatus.Pending));

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            sql += """
                 AND (c.OldAppraisalReportNumber LIKE @Search
                      OR c.NormalizedSurveyNumber LIKE @Search
                      OR c.CifNumber LIKE @Search
                      OR c.CustomerName LIKE @Search)
                """;
            p.Add("Search", $"%{EscapeLike(query.Search.Trim())}%");
        }

        switch (query.PriorSource?.Trim())
        {
            case "CAS" or "AS400Legacy" or "Unknown":
                sql += " AND c.PriorAppraisalSource = @PriorSource";
                p.Add("PriorSource", query.PriorSource.Trim());
                break;
            case "NonCAS":
                sql += " AND c.PriorAppraisalSource <> 'CAS'";
                break;
        }

        if (consumed && ProcessedBooksSql.StatePredicate(query.NewAppraisalState) is { } state)
            sql += $" AND {state}";

        // Due date and "in progress" describe the to-do tabs; the processed tab does not select them.
        if (!consumed && query.InProgress.HasValue)
        {
            sql += " AND c.HasOpenAppraisal = @InProgress";
            p.Add("InProgress", query.InProgress.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.CustomerName))
        {
            sql += " AND c.CustomerName LIKE @CustomerName";
            p.Add("CustomerName", $"%{query.CustomerName.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(query.OldAppraisalReportNumber))
        {
            sql += " AND c.OldAppraisalReportNumber LIKE @OldAppraisalReportNumber";
            p.Add("OldAppraisalReportNumber", $"%{query.OldAppraisalReportNumber.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(query.CifNumber))
        {
            sql += " AND c.CifNumber = @CifNumber";
            p.Add("CifNumber", query.CifNumber.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.CollateralId))
        {
            sql += " AND c.CollateralId = @CollateralId";
            p.Add("CollateralId", query.CollateralId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.ReviewType))
        {
            sql += " AND c.ReviewType = @ReviewType";
            p.Add("ReviewType", query.ReviewType.Trim());
        }

        // The review-date range filters on the due date (DueDate), what the list shows and counts down to —
        // the to-do tabs only, like RemainingDay: the processed tab shows no due date.
        if (!consumed && query.ReviewDateFrom.HasValue)
        {
            sql += " AND c.DueDate >= @ReviewDateFrom";
            p.Add("ReviewDateFrom", query.ReviewDateFrom.Value.ToDateTime(TimeOnly.MinValue));
        }

        if (!consumed && query.ReviewDateTo.HasValue)
        {
            sql += " AND c.DueDate <= @ReviewDateTo";
            // Midnight, not TimeOnly.MaxValue: DueDate is a date, and 23:59:59.9999999 sent as datetime
            // rounds up to the next day's midnight — the filter would take one day too many.
            p.Add("ReviewDateTo", query.ReviewDateTo.Value.ToDateTime(TimeOnly.MinValue));
        }

        if (!consumed && query.RemainingDayFrom.HasValue)
        {
            sql += " AND c.RemainingDay >= @RemainingDayFrom";
            p.Add("RemainingDayFrom", query.RemainingDayFrom.Value);
        }

        if (!consumed && query.RemainingDayTo.HasValue)
        {
            sql += " AND c.RemainingDay <= @RemainingDayTo";
            p.Add("RemainingDayTo", query.RemainingDayTo.Value);
        }

        var result = await connectionFactory.GetOpenConnection()
            .QueryPaginatedAsync<ReappraisalCandidateListItem>(
                sql,
                orderBy: consumed
                    ? BuildOrderBy(query.SortBy, query.SortDir, ConsumedSortableColumns, ConsumedDefaultOrderBy)
                    : BuildOrderBy(query.SortBy, query.SortDir, SortableColumns, DefaultOrderBy),
                request: query.Pagination,
                param: p);

        return new GetReappraisalCandidatesResult(result);
    }

    // Due first: the book closest to (or furthest past) the review due date (DueDate) leads; one with no
    // due date goes last (SQL Server puts NULL first on ASC). c.Id last: AS400 sets due dates in batches,
    // so without a unique key OFFSET/FETCH could repeat or skip rows across pages.
    private const string DefaultOrderBy =
        "CASE WHEN c.RemainingDay IS NULL THEN 1 ELSE 0 END, c.RemainingDay ASC, c.CifNumber ASC, c.Id ASC";

    // Processed tab: the latest submission first; a book with no reappraisal found goes last.
    private const string ConsumedDefaultOrderBy =
        "CASE WHEN na.SubmittedAt IS NULL THEN 1 ELSE 0 END, na.SubmittedAt DESC, c.CifNumber ASC, c.Id ASC";

    private static readonly Dictionary<string, string> ConsumedSortableColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["OldAppraisalReportNumber"] = "c.OldAppraisalReportNumber",
        ["CifNumber"] = "c.CifNumber",
        ["CustomerName"] = "c.CustomerName",
        ["ReviewType"] = "c.ReviewType",
        ["NewAppraisalSubmittedAt"] = "na.SubmittedAt",
        ["NewAppraisalCompletedAt"] = "na.CompletedAt",
    };

    private const string ConsumedSql = """
        SELECT
            c.Id,
            c.Status,
            c.ReviewType,
            c.OldAppraisalReportNumber,
            c.CifNumber,
            c.CustomerName,
            c.CollateralId,
            c.CollateralName,
            c.PriorAppraisalSource,
            c.FirstSeenFileDate,
            c.LastSeenFileDate,
            na.AppraisalId     AS NewAppraisalId,
            na.AppraisalNumber AS NewAppraisalNumber,
            na.Status          AS NewAppraisalStatus,
            na.GroupTag        AS NewAppraisalGroupTag,
            na.SubmittedAt     AS NewAppraisalSubmittedAt,
            na.CompletedAt     AS NewAppraisalCompletedAt,
            c.IsBlockUnit,
            un.MatchedUnits    AS UnitMatchedUnits,
            un.TowerName       AS UnitTowerName,
            un.Floor           AS UnitFloor,
            un.RoomNumber      AS UnitRoomNumber,
            un.HouseNumber     AS UnitHouseNumber,
            un.PlotNumber      AS UnitPlotNumber
        """ + "\n" + ProcessedBooksSql.From;  // a raw literal drops its last newline

    /// <summary>LIKE wildcards typed into the search box match themselves.</summary>
    private static string EscapeLike(string s) =>
        s.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");

    /// <summary>
    /// Maps a client-supplied sort field to a whitelisted view column. The pagination
    /// helper only blocks injection characters — it does NOT validate column names — so
    /// unknown fields fall back to the default order rather than being interpolated raw.
    /// </summary>
    private static readonly Dictionary<string, string> SortableColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["OldAppraisalReportNumber"] = "c.OldAppraisalReportNumber",
        ["CifNumber"] = "c.CifNumber",
        ["CustomerName"] = "c.CustomerName",
        ["ReviewType"] = "c.ReviewType",
        ["RemainingDay"] = "c.RemainingDay",
        ["DueDate"] = "c.DueDate",
        ["AppraisalDate"] = "c.AppraisalDate",
    };

    private static string BuildOrderBy(
        string? sortBy, string? sortDir, Dictionary<string, string> columns, string defaultOrderBy)
    {
        if (string.IsNullOrWhiteSpace(sortBy) ||
            !columns.TryGetValue(sortBy.Trim(), out var column))
        {
            return defaultOrderBy;
        }

        var direction = string.Equals(sortDir?.Trim(), "desc", StringComparison.OrdinalIgnoreCase)
            ? "DESC"
            : "ASC";

        // Blanks last in either direction (SQL Server puts NULL first on ASC): a book with no value in
        // the sorted column (e.g. no traceable last appraisal) must not head the list.
        // Keep CifNumber, then the unique Id, as tiebreakers so paging is stable — CifNumber not when it's
        // already the sort column (SQL Server rejects a column appearing twice in ORDER BY).
        var nullsLast = $"CASE WHEN {column} IS NULL THEN 1 ELSE 0 END, {column} {direction}";
        return column == "c.CifNumber" ? $"{nullsLast}, c.Id ASC" : $"{nullsLast}, c.CifNumber ASC, c.Id ASC";
    }
}
