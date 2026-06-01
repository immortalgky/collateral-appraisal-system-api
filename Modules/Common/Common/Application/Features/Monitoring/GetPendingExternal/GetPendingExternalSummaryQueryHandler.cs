using Common.Application.Features.Monitoring.Shared;
using Dapper;
using Shared.CQRS;
using Shared.Data;

namespace Common.Application.Features.Monitoring.GetPendingExternal;

/// <summary>
/// Returns KPI bucket counts for the Pending External monitoring tab.
/// Mirrors GetPendingInternalSummaryQueryHandler with MonitoringType = 'External'
/// and external-specific scope / search fields.
/// </summary>
public class GetPendingExternalSummaryQueryHandler(
    ISqlConnectionFactory connectionFactory,
    MonitoringScopeService scopeService)
    : IQueryHandler<GetPendingExternalSummaryQuery, MonitoringSummaryDto>
{
    public async Task<MonitoringSummaryDto> Handle(
        GetPendingExternalSummaryQuery query,
        CancellationToken cancellationToken)
    {
        var scope = scopeService.ResolveExternalScope();

        var conditions = new List<string> { "MonitoringType = 'External'" };
        var parameters = new DynamicParameters();

        if (!scopeService.TryBuildActivityFilter(scope, conditions, parameters))
            return new MonitoringSummaryDto(0, 0, 0, 0);

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
            conditions.Add("(AppraisalNumber LIKE @Search ESCAPE '\\' OR CustomerName LIKE @Search ESCAPE '\\' OR AppraisalCompanyName LIKE @Search ESCAPE '\\' OR AssignedTo LIKE @Search ESCAPE '\\')");
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

        if (filter.AppraisalCompanyId is { Length: > 0 })
        {
            conditions.Add("AppraisalCompanyId IN @AppraisalCompanyIds");
            parameters.Add("AppraisalCompanyIds", filter.AppraisalCompanyId);
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
    /// Unknown values are silently ignored.
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
