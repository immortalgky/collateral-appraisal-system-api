using Dapper;
using Shared.CQRS;
using Shared.Data;
using Shared.Pagination;

namespace Common.Application.Features.Monitoring.GetMeetingFollowups;

/// <summary>
/// Returns pending committee-approval tasks from common.vw_MonitoringPendingApprovals.
/// One row per appraisal; visible to any caller holding the MeetingFollowup permission.
/// </summary>
public class GetMeetingFollowupsQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetMeetingFollowupsQuery, PaginatedResult<MeetingFollowupDto>>
{
    private static readonly HashSet<string> AllowedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "AppraisalNumber", "CustomerName", "ApprovalTier", "PendingCount",
        "TotalApprovers", "MeetingDate"
    };

    public async Task<PaginatedResult<MeetingFollowupDto>> Handle(
        GetMeetingFollowupsQuery query,
        CancellationToken cancellationToken)
    {
        // Explicit projection — column order MUST match MeetingFollowupDto's positional record constructor.
        var sql = @"
SELECT
    AppraisalId,
    AppraisalNumber,
    CustomerName,
    ApprovalTier,
    PendingCount,
    TotalApprovers,
    EarliestDueAt,
    WorstSlaStatus,
    MeetingId,
    MeetingNumber,
    MeetingDate,
    MeetingStatus
FROM common.vw_MonitoringPendingApprovals";

        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        var filter = query.Filter;

        if (filter.Tier is { Length: > 0 })
        {
            conditions.Add("ApprovalTier IN @Tiers");
            parameters.Add("Tiers", filter.Tier);
        }

        if (filter.SlaStatus is { Length: > 0 })
        {
            conditions.Add("WorstSlaStatus IN @SlaStatuses");
            parameters.Add("SlaStatuses", filter.SlaStatus);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            conditions.Add("(AppraisalNumber LIKE @Search ESCAPE '\\' OR CustomerName LIKE @Search ESCAPE '\\')");
            parameters.Add("Search", "%" + EscapeLike(filter.Search.Trim()) + "%");
        }

        if (!string.IsNullOrWhiteSpace(filter.MeetingNumber))
        {
            conditions.Add("MeetingNumber LIKE @MeetingNumber ESCAPE '\\'");
            parameters.Add("MeetingNumber", "%" + EscapeLike(filter.MeetingNumber.Trim()) + "%");
        }

        // Dapper rejects DateOnly — convert to DateTime (memory: feedback_dapper_dateonly).
        if (filter.MeetingDateFrom is { } from)
        {
            conditions.Add("MeetingDate >= @MeetingDateFrom");
            parameters.Add("MeetingDateFrom", from.ToDateTime(TimeOnly.MinValue));
        }

        if (filter.MeetingDateTo is { } to)
        {
            conditions.Add("MeetingDate <= @MeetingDateTo");
            parameters.Add("MeetingDateTo", to.ToDateTime(TimeOnly.MaxValue));
        }

        AppendSlaBucketCondition(filter.SlaBucket, conditions);

        if (conditions.Count > 0)
            sql += " WHERE " + string.Join(" AND ", conditions);

        var sortField = AllowedSortFields.Contains(filter.SortBy ?? "") ? filter.SortBy! : "AppraisalNumber";
        var sortDir = string.Equals(filter.SortDir, "asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";
        var orderBy = $"{sortField} {sortDir}";

        return await connectionFactory.QueryPaginatedAsync<MeetingFollowupDto>(sql, orderBy, query.Paging, parameters);
    }

    /// <summary>
    /// Maps SlaBucket filter values to WorstSlaStatus column conditions.
    /// 'healthy' maps to 'OnTime' because the view uses that label for the lowest severity.
    /// </summary>
    private static void AppendSlaBucketCondition(string[]? buckets, List<string> conditions)
    {
        if (buckets is not { Length: > 0 }) return;
        var ors = new List<string>();
        foreach (var b in buckets)
        {
            switch (b.ToLowerInvariant())
            {
                case "breached": ors.Add("WorstSlaStatus = 'Breached'"); break;
                case "atrisk":   ors.Add("WorstSlaStatus = 'AtRisk'");   break;
                case "healthy":  ors.Add("WorstSlaStatus = 'OnTime'");   break;
            }
        }
        if (ors.Count > 0) conditions.Add("(" + string.Join(" OR ", ors) + ")");
    }

    // Escapes SQL Server LIKE wildcards so user input matches literally; paired with ESCAPE '\'.
    private static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
}
