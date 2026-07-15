using Common.Application.Features.Monitoring.GetPendingInternal;
using Common.Application.Features.Monitoring.Shared;
using Dapper;
using Shared.CQRS;
using Shared.Data;
using Shared.Pagination;

namespace Common.Application.Features.Monitoring.GetPendingExternal;

/// <summary>
/// Returns External pending tasks from common.vw_MonitoringPendingTasks.
/// Scoped by the user's held layer permissions via MonitoringScopeService.
/// Reuses PendingTaskDto from GetPendingInternal (same view, same shape).
/// </summary>
public class GetPendingExternalQueryHandler(
    ISqlConnectionFactory connectionFactory,
    MonitoringScopeService scopeService)
    : IQueryHandler<GetPendingExternalQuery, PaginatedResult<PendingTaskDto>>
{
    private static readonly HashSet<string> AllowedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "AppraisalNumber", "CustomerName", "TaskType", "Purpose", "PropertyType",
        "SlaStatus", "Priority", "AssignedDate", "RequestedDate",
        "OlaActualHours", "OlaVarianceHours", "Movement", "PIC", "AppraisalCompanyName",
        "OpenDate", "AppointmentDate", "AppraisalStatus"
    };

    public async Task<PaginatedResult<PendingTaskDto>> Handle(
        GetPendingExternalQuery query,
        CancellationToken cancellationToken)
    {
        var scope = scopeService.ResolveExternalScope();

        // Explicit projection — column order MUST match PendingTaskDto's positional record constructor.
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
    AssignedType,
    OpenDate,
    AppointmentDate,
    SlaDurationHours,
    AppraisalStatus
FROM common.vw_MonitoringPendingTasks";
        var conditions = new List<string> { "MonitoringType = 'External'" };
        var parameters = new DynamicParameters();

        // Activity + team scope filter. Empty scope ⇒ user holds no external monitoring permission.
        if (!scopeService.TryBuildActivityFilter(scope, conditions, parameters))
            return new PaginatedResult<PendingTaskDto>([], 0, query.Paging.PageNumber, query.Paging.PageSize);

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
            conditions.Add("AssignedTo = @Pic");
            parameters.Add("Pic", filter.Pic.Trim());
            if (!string.IsNullOrWhiteSpace(filter.PicType))
            {
                conditions.Add("AssignedType = @PicType");
                parameters.Add("PicType", filter.PicType.Trim());
            }
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
