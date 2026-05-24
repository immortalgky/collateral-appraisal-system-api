using Common.Application.Features.Monitoring.Shared;
using Dapper;
using Shared.CQRS;
using Shared.Data;

namespace Common.Application.Features.Monitoring.GetMeetingFollowups;

/// <summary>
/// Returns KPI bucket counts for the Pending Approval Followup monitoring tab.
/// Runs a single aggregate query against common.vw_MonitoringPendingApprovals with the
/// same WHERE clause as the list handler — so summary.Total always matches list count
/// under the same filter.
///
/// Bucket mapping:
///   Breached : WorstSlaStatus = 'Breached'
///   AtRisk   : WorstSlaStatus = 'AtRisk'
///   Healthy  : WorstSlaStatus = 'OnTime'  (view uses 'OnTime' label for the lowest severity)
/// </summary>
public class GetMeetingFollowupsSummaryQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetMeetingFollowupsSummaryQuery, MonitoringSummaryDto>
{
    public async Task<MonitoringSummaryDto> Handle(
        GetMeetingFollowupsSummaryQuery query,
        CancellationToken cancellationToken)
    {
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

        var where = conditions.Count > 0
            ? "WHERE " + string.Join(" AND ", conditions)
            : string.Empty;

        var sql = $@"
SELECT
    COUNT(*)                                                            AS Total,
    SUM(CASE WHEN WorstSlaStatus = 'Breached' THEN 1 ELSE 0 END)       AS Breached,
    SUM(CASE WHEN WorstSlaStatus = 'AtRisk'   THEN 1 ELSE 0 END)       AS AtRisk,
    SUM(CASE WHEN WorstSlaStatus = 'OnTime'   THEN 1 ELSE 0 END)       AS Healthy
FROM common.vw_MonitoringPendingApprovals
{where}";

        var conn = connectionFactory.GetOpenConnection();
        var row = await conn.QuerySingleAsync<SummaryRow>(sql, parameters);
        return new MonitoringSummaryDto(row.Total, row.Breached, row.AtRisk, row.Healthy);
    }

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

    private static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");

    private record SummaryRow(int Total, int Breached, int AtRisk, int Healthy);
}
