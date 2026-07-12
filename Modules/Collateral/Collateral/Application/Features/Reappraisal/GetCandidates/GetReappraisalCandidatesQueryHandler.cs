using Dapper;

namespace Collateral.Application.Features.Reappraisal.GetCandidates;

/// <summary>
/// Read-side handler for the Reappraisal Candidates list page.
/// Uses Dapper + DynamicParameters against collateral.vw_ReappraisalCandidates (Status &lt;&gt; 'Deleted').
/// </summary>
public class GetReappraisalCandidatesQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetReappraisalCandidatesQuery, GetReappraisalCandidatesResult>
{
    public async Task<GetReappraisalCandidatesResult> Handle(
        GetReappraisalCandidatesQuery query,
        CancellationToken cancellationToken)
    {
        var sql = """
            SELECT
                c.Id,
                c.Status,
                c.ReviewType,
                c.AppraisalDate,
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
                c.OpenAppraisalGroupTag
            FROM collateral.vw_ReappraisalCandidates c
            WHERE c.Status = 'Pending'
            """;

        var p = new DynamicParameters();

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

        if (query.ReviewDateFrom.HasValue)
        {
            sql += " AND c.ReviewDate >= @ReviewDateFrom";
            p.Add("ReviewDateFrom", query.ReviewDateFrom.Value.ToDateTime(TimeOnly.MinValue));
        }

        if (query.ReviewDateTo.HasValue)
        {
            sql += " AND c.ReviewDate <= @ReviewDateTo";
            p.Add("ReviewDateTo", query.ReviewDateTo.Value.ToDateTime(TimeOnly.MaxValue));
        }

        if (query.RemainingDayFrom.HasValue)
        {
            sql += " AND c.RemainingDay >= @RemainingDayFrom";
            p.Add("RemainingDayFrom", query.RemainingDayFrom.Value);
        }

        if (query.RemainingDayTo.HasValue)
        {
            sql += " AND c.RemainingDay <= @RemainingDayTo";
            p.Add("RemainingDayTo", query.RemainingDayTo.Value);
        }

        var result = await connectionFactory.GetOpenConnection()
            .QueryPaginatedAsync<ReappraisalCandidateListItem>(
                sql,
                orderBy: BuildOrderBy(query.SortBy, query.SortDir),
                request: query.Pagination,
                param: p);

        return new GetReappraisalCandidatesResult(result);
    }

    private const string DefaultOrderBy = "c.ReviewDate ASC, c.CifNumber ASC";

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
        ["AppraisalDate"] = "c.AppraisalDate",
    };

    private static string BuildOrderBy(string? sortBy, string? sortDir)
    {
        if (string.IsNullOrWhiteSpace(sortBy) ||
            !SortableColumns.TryGetValue(sortBy.Trim(), out var column))
        {
            return DefaultOrderBy;
        }

        var direction = string.Equals(sortDir?.Trim(), "desc", StringComparison.OrdinalIgnoreCase)
            ? "DESC"
            : "ASC";

        // Keep CifNumber as a stable tiebreaker, but not when it's already the sort column
        // (SQL Server rejects a column appearing twice in ORDER BY).
        return column == "c.CifNumber"
            ? $"{column} {direction}"
            : $"{column} {direction}, c.CifNumber ASC";
    }
}
