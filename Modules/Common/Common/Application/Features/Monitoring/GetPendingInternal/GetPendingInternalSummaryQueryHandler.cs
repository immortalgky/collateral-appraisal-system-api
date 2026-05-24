using Common.Application.Features.Monitoring.Shared;
using Dapper;
using Shared.CQRS;
using Shared.Data;

namespace Common.Application.Features.Monitoring.GetPendingInternal;

/// <summary>
/// Returns KPI bucket counts for the Pending Internal monitoring tab.
/// Runs a single aggregate query against common.vw_MonitoringPendingTasks with the
/// same WHERE clause as the list handler — so summary.Total always matches list count
/// under the same filter.
///
/// Bucket thresholds (Decision E1):
///   Breached : OlaVarianceHours > 0
///   AtRisk   : OlaVarianceHours &lt;= 0 AND remaining &lt;= 25% of target AND OlaTargetHours > 0
///   Healthy  : everything else (including OlaTargetHours = 0 — no SLA configured)
/// </summary>
public class GetPendingInternalSummaryQueryHandler(
    ISqlConnectionFactory connectionFactory,
    MonitoringScopeService scopeService)
    : IQueryHandler<GetPendingInternalSummaryQuery, MonitoringSummaryDto>
{
    public async Task<MonitoringSummaryDto> Handle(
        GetPendingInternalSummaryQuery query,
        CancellationToken cancellationToken)
    {
        var activityIds = scopeService.GetInternalActivityIds();
        if (activityIds.Length == 0)
            return new MonitoringSummaryDto(0, 0, 0, 0);

        var conditions = new List<string> { "MonitoringType = 'Internal'" };
        var parameters = new DynamicParameters();

        conditions.Add("ActivityId IN @ActivityIds");
        parameters.Add("ActivityIds", activityIds);

        var filter = query.Filter;
        if (filter.SlaStatus is { Length: > 0 })
        {
            conditions.Add("SlaStatus IN @SlaStatuses");
            parameters.Add("SlaStatuses", filter.SlaStatus);
        }

        if (filter.ActivityId is { Length: > 0 })
        {
            conditions.Add("ActivityId IN @FilterActivityIds");
            parameters.Add("FilterActivityIds", filter.ActivityId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            conditions.Add("(AppraisalNumber LIKE @Search ESCAPE '\\' OR CustomerName LIKE @Search ESCAPE '\\' OR AssignedTo LIKE @Search ESCAPE '\\')");
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

        var sql = $@"
SELECT
    COUNT(*) AS Total,
    SUM(CASE WHEN OlaVarianceHours > 0 THEN 1 ELSE 0 END) AS Breached,
    SUM(CASE WHEN OlaVarianceHours <= 0
              AND OlaTargetHours > 0
              AND (OlaTargetHours - OlaActualHours) <= OlaTargetHours * 0.25
             THEN 1 ELSE 0 END) AS AtRisk,
    SUM(CASE
          WHEN OlaVarianceHours IS NULL THEN 1
          WHEN OlaVarianceHours <= 0
               AND (OlaTargetHours = 0
                    OR (OlaTargetHours - OlaActualHours) > OlaTargetHours * 0.25) THEN 1
          ELSE 0
        END) AS Healthy
FROM common.vw_MonitoringPendingTasks
{where}";

        var conn = connectionFactory.GetOpenConnection();
        var row = await conn.QuerySingleAsync<SummaryRow>(sql, parameters);
        return new MonitoringSummaryDto(row.Total, row.Breached, row.AtRisk, row.Healthy);
    }

    /// <summary>
    /// Appends a WHERE condition matching the computed OLA bucket.
    /// NULL OlaVarianceHours is treated as Healthy (no SLA configured).
    /// Unknown values are silently ignored — consistent with how SlaStatus filter behaves.
    /// </summary>
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

    private record SummaryRow(int Total, int Breached, int AtRisk, int Healthy);
}
