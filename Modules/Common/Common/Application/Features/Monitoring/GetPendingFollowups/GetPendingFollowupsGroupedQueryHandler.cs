using Common.Application.Features.Monitoring.Shared;
using Dapper;
using Shared.CQRS;
using Shared.Data;

namespace Common.Application.Features.Monitoring.GetPendingFollowups;

/// <summary>
/// Returns grouped counts for the Pending Followups tab.
/// Sources from common.vw_MonitoringPendingTasks filtered to the two followup activity IDs.
/// groupBy mapping:
///   "pic"      → AssignedTo (key) / MAX(PIC) (label) — matches Internal convention
///   "company"  → AppraisalCompanyName
///   "activity" → ActivityId
/// </summary>
public class GetPendingFollowupsGroupedQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetPendingFollowupsGroupedQuery, MonitoringGroupedResult>
{
    public async Task<MonitoringGroupedResult> Handle(
        GetPendingFollowupsGroupedQuery query,
        CancellationToken cancellationToken)
    {
        var conditions = new List<string> { "ActivityId IN @ActivityIds" };
        var parameters = new DynamicParameters();
        parameters.Add("ActivityIds", MonitoringActivityMap.Followup);

        var filter = query.Filter;
        if (filter.SlaStatus is { Length: > 0 })
        {
            conditions.Add("SlaStatus IN @SlaStatuses");
            parameters.Add("SlaStatuses", filter.SlaStatus);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            conditions.Add("(AppraisalNumber LIKE @Search ESCAPE '\\' OR CustomerName LIKE @Search ESCAPE '\\' OR AppraisalCompanyName LIKE @Search ESCAPE '\\')");
            parameters.Add("Search", "%" + EscapeLike(filter.Search.Trim()) + "%");
        }

        AppendSlaBucketCondition(filter.SlaBucket, conditions);

        if (!string.IsNullOrWhiteSpace(filter.Pic))
        {
            conditions.Add("PIC LIKE @Pic ESCAPE '\\'");
            parameters.Add("Pic", "%" + EscapeLike(filter.Pic.Trim()) + "%");
        }

        if (filter.Purpose is { Length: > 0 })
        {
            conditions.Add("Purpose IN @Purposes");
            parameters.Add("Purposes", filter.Purpose);
        }

        if (filter.PropertyType is { Length: > 0 })
        {
            conditions.Add("PropertyType IN @PropertyTypes");
            parameters.Add("PropertyTypes", filter.PropertyType);
        }

        if (filter.TaskType is { Length: > 0 })
        {
            conditions.Add("TaskType IN @TaskTypes");
            parameters.Add("TaskTypes", filter.TaskType);
        }

        var where = "WHERE " + string.Join(" AND ", conditions);
        var (keyExpr, labelExpr, groupByExpr) = ResolveGrouping(query.GroupBy);

        var sql = $@"
SELECT
    {keyExpr} AS [Key],
    {labelExpr} AS Label,
    COUNT(*) AS Count,
    SUM(CASE WHEN OlaVarianceHours > 0 THEN 1 ELSE 0 END) AS Breached,
    SUM(CASE WHEN OlaVarianceHours <= 0
              AND OlaTargetHours > 0
              AND (OlaTargetHours - OlaActualHours) <= OlaTargetHours * 0.25
             THEN 1 ELSE 0 END) AS AtRisk
FROM common.vw_MonitoringPendingTasks
{where}
GROUP BY {groupByExpr}
ORDER BY COUNT(*) DESC
OFFSET 0 ROWS FETCH NEXT 200 ROWS ONLY;

SELECT COUNT(*) AS Total
FROM common.vw_MonitoringPendingTasks
{where}";

        var conn = connectionFactory.GetOpenConnection();
        using var multi = await conn.QueryMultipleAsync(sql, parameters);

        var groups = (await multi.ReadAsync<GroupRow>()).ToList();
        var total = await multi.ReadSingleAsync<int>();

        var result = groups
            .Select(g => new MonitoringGroupRow(
                g.Key ?? string.Empty,
                g.Label ?? string.Empty,
                g.Count,
                g.Breached,
                g.AtRisk))
            .ToList();

        return new MonitoringGroupedResult(result, total);
    }

    private static (string keyExpr, string labelExpr, string groupByExpr) ResolveGrouping(string groupBy) =>
        groupBy.ToLowerInvariant() switch
        {
            "pic" => (
                "AssignedTo",
                "MAX(PIC)",
                "AssignedTo"),
            "company" => (
                "COALESCE(NULLIF(AppraisalCompanyName, ''), 'Unassigned')",
                "COALESCE(NULLIF(AppraisalCompanyName, ''), 'Unassigned')",
                "COALESCE(NULLIF(AppraisalCompanyName, ''), 'Unassigned')"),
            "activity" => (
                "ActivityId",
                "ActivityId",
                "ActivityId"),
            _ => throw new ArgumentException($"Unsupported groupBy value '{groupBy}'. Allowed: pic, company, activity.", nameof(groupBy))
        };

    private static void AppendSlaBucketCondition(string[]? buckets, List<string> conditions)
    {
        if (buckets is not { Length: > 0 }) return;
        var ors = new List<string>();
        foreach (var b in buckets)
        {
            switch (b.ToLowerInvariant())
            {
                case "breached": ors.Add("OlaVarianceHours > 0"); break;
                case "atrisk":   ors.Add("OlaVarianceHours <= 0 AND OlaTargetHours > 0 AND (OlaTargetHours - OlaActualHours) <= OlaTargetHours * 0.25"); break;
                case "healthy":  ors.Add("(OlaVarianceHours IS NULL OR (OlaVarianceHours <= 0 AND (OlaTargetHours = 0 OR (OlaTargetHours - OlaActualHours) > OlaTargetHours * 0.25)))"); break;
            }
        }
        if (ors.Count > 0) conditions.Add("(" + string.Join(" OR ", ors) + ")");
    }

    private static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");

    private record GroupRow(string? Key, string? Label, int Count, int Breached, int AtRisk);
}
