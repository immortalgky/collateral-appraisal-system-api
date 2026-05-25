using Common.Application.Features.Monitoring.Shared;
using Dapper;
using Shared.CQRS;
using Shared.Data;
using Shared.Pagination;

namespace Common.Application.Features.Monitoring.GetPendingInternal;

/// <summary>
/// Returns Internal pending tasks from common.vw_MonitoringPendingTasks.
/// Scoped by the user's held layer permissions via MonitoringScopeService.
/// </summary>
public class GetPendingInternalQueryHandler(
    ISqlConnectionFactory connectionFactory,
    MonitoringScopeService scopeService)
    : IQueryHandler<GetPendingInternalQuery, PaginatedResult<PendingTaskDto>>
{
    private static readonly HashSet<string> AllowedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "AppraisalNumber", "CustomerName", "TaskType", "Purpose", "PropertyType",
        "SlaStatus", "Priority", "AssignedDate", "RequestedDate",
        "OlaActualHours", "OlaVarianceHours", "Movement", "PIC"
    };

    public async Task<PaginatedResult<PendingTaskDto>> Handle(
        GetPendingInternalQuery query,
        CancellationToken cancellationToken)
    {
        var activityIds = scopeService.GetInternalActivityIds();
        // If user holds no internal monitoring permission, return empty result
        if (activityIds.Length == 0)
            return new PaginatedResult<PendingTaskDto>([], 0, query.Paging.PageNumber, query.Paging.PageSize);

        // Explicit projection: column order MUST match PendingTaskDto's positional record constructor.
        // SELECT * fails because the view exposes more columns (AssignedTo, AssignedType, AssigneeCompanyId,
        // AppraisalCompanyId) than the DTO's constructor accepts — Dapper records require exact arity.
        var sql = @"
SELECT
    PendingTaskId,
    AppraisalId,
    AppraisalNumber,
    CustomerName,
    TaskType,
    TaskDescription,
    Purpose,
    PropertyType,
    SlaStatus,
    Priority,
    RequestedDate,
    AssignedDate,
    PIC,
    Movement,
    OlaTargetHours,
    OlaActualHours,
    OlaVarianceHours,
    ActivityId,
    AppraisalCompanyName,
    MonitoringType,
    AssignedTo,
    AssignedType
FROM common.vw_MonitoringPendingTasks";
        var conditions = new List<string> { "MonitoringType = 'Internal'" };
        var parameters = new DynamicParameters();

        // Activity-ID scope filter (user's layer permissions → activity IDs)
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
            // Caller may further narrow to specific activities within their allowed set
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

        sql += " WHERE " + string.Join(" AND ", conditions);

        var sortField = AllowedSortFields.Contains(filter.SortBy ?? "") ? filter.SortBy! : "AssignedDate";
        var sortDir = string.Equals(filter.SortDir, "asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";
        var orderBy = $"{sortField} {sortDir}";

        return await connectionFactory.QueryPaginatedAsync<PendingTaskDto>(sql, orderBy, query.Paging, parameters);
    }

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

    // Escapes SQL Server LIKE wildcards so user input matches literally; paired with ESCAPE '\'.
    private static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
}
