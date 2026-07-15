using Dapper;
using Shared.Data;
using Shared.Identity;
using Shared.Pagination;
using Workflow.Contracts.Sla;
using Shared.Time;

namespace Workflow.Tasks.Features.GetMonitoredTasks;

public class GetMonitoredTasksQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUserService,
    ITaskMonitorScope taskMonitorScope,
    IBusinessTimeCalculator businessTime,
    IDateTimeProvider clock
) : IQueryHandler<GetMonitoredTasksQuery, GetMonitoredTasksResult>
{
    private static readonly HashSet<string> AllowedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "AssignedAt", "DueAt", "SlaStatus", "ElapsedHours", "RemainingHours",
        "AppraisalNumber", "CustomerName", "TaskName", "AssignedTo", "GroupName",
        "Purpose", "AppraisalStatus"
    };

    public async Task<GetMonitoredTasksResult> Handle(
        GetMonitoredTasksQuery query, CancellationToken cancellationToken)
    {
        var currentUser = currentUserService.Username;

        var sql = "SELECT * FROM workflow.vw_TaskMonitor";
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        // Base filter: only tasks in groups monitored by the current user
        conditions.Add(
            """
            AssignedTo IN (
                SELECT tgtU.UserName
                FROM auth.GroupMonitoring gm
                INNER JOIN auth.GroupUsers supGu ON supGu.GroupId = gm.MonitorGroupId
                INNER JOIN auth.AspNetUsers supU ON supU.Id = supGu.UserId
                    AND supU.NormalizedUserName = @SupervisorNormalizedUserName
                INNER JOIN auth.Groups supG ON supG.Id = gm.MonitorGroupId AND supG.IsDeleted = 0
                INNER JOIN auth.Groups tgtG ON tgtG.Id = gm.MonitoredGroupId AND tgtG.IsDeleted = 0
                INNER JOIN auth.GroupUsers tgtGu ON tgtGu.GroupId = gm.MonitoredGroupId
                INNER JOIN auth.AspNetUsers tgtU ON tgtU.Id = tgtGu.UserId
            )
            """);
        parameters.Add("SupervisorNormalizedUserName", currentUser?.ToUpperInvariant() ?? "");

        // Additional :TEAM scope: when the user holds only TASK_MONITOR_VIEW:TEAM (not the base
        // permission), confine the list to their own team (internal) or company (external).
        var teamScopeClause = taskMonitorScope.BuildScopeClause("TASK_MONITOR_VIEW", parameters);
        if (teamScopeClause is not null)
            conditions.Add(teamScopeClause);

        var filter = query.Filter;
        if (filter is not null)
        {
            if (filter.GroupId is { Length: > 0 })
            {
                conditions.Add("GroupId IN @GroupIds");
                parameters.Add("GroupIds", filter.GroupId);
            }

            if (filter.AssigneeUsername is { Length: > 0 })
            {
                conditions.Add("AssignedTo IN @AssigneeUsernames");
                parameters.Add("AssigneeUsernames", filter.AssigneeUsername);
            }

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

            if (!string.IsNullOrWhiteSpace(filter.AppraisalNumber))
            {
                conditions.Add("AppraisalNumber LIKE '%' + @AppraisalNumber + '%'");
                parameters.Add("AppraisalNumber", filter.AppraisalNumber);
            }

            if (!string.IsNullOrWhiteSpace(filter.CustomerName))
            {
                conditions.Add("CustomerName LIKE '%' + @CustomerName + '%'");
                parameters.Add("CustomerName", filter.CustomerName);
            }

            if (filter.AppraisalStatus is { Length: > 0 })
            {
                conditions.Add("AppraisalStatus IN @AppraisalStatuses");
                parameters.Add("AppraisalStatuses", filter.AppraisalStatus);
            }

            if (filter.TaskType is { Length: > 0 })
            {
                conditions.Add("TaskDescription IN @TaskTypes");
                parameters.Add("TaskTypes", filter.TaskType);
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                conditions.Add("(AppraisalNumber LIKE '%' + @Search + '%' OR CustomerName LIKE '%' + @Search + '%' OR AssignedTo LIKE '%' + @Search + '%')");
                parameters.Add("Search", filter.Search);
            }
        }

        if (conditions.Count > 0)
            sql += " WHERE " + string.Join(" AND ", conditions);

        var sortField = AllowedSortFields.Contains(filter?.SortBy ?? "") ? filter!.SortBy! : "AssignedAt";
        var sortDir = string.Equals(filter?.SortDir, "asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";

        // ElapsedHours/RemainingHours are no longer columns on vw_TaskMonitor (computed in C# via
        // IBusinessTimeCalculator). Their business-time values are monotonic in the underlying
        // timestamps, so translate the sort for exact ordering — and to avoid ORDER BY referencing
        // a dropped column:
        //   ElapsedHours  ASC  ≡ AssignedAt DESC (least elapsed = most recent assignment)
        //   RemainingHours ASC ≡ DueAt      ASC  (least remaining = earliest due)
        var orderBy = sortField switch
        {
            "ElapsedHours" => $"AssignedAt {Invert(sortDir)}",
            "RemainingHours" => $"DueAt {sortDir}",
            _ => $"{sortField} {sortDir}"
        };

        var result = await connectionFactory.QueryPaginatedAsync<MonitoredTaskDto>(
            sql, orderBy, query.PaginationRequest, parameters);

        // Business-time Elapsed/Remaining: exclude weekends, holidays and lunch via the shared
        // calculator. Only the returned page is recomputed; the calculator caches config/holidays.
        var now = clock.ApplicationNow;
        var items = new List<MonitoredTaskDto>();
        foreach (var t in result.Items)
        {
            var (elapsed, remaining) =
                await businessTime.ComputeElapsedRemainingHoursAsync(now, t.SlaStartAt ?? t.AssignedAt, t.DueAt, clockStart: t.SlaStartAt, ct: cancellationToken);
            items.Add(t with { ElapsedHours = elapsed, RemainingHours = remaining });
        }

        var paged = new PaginatedResult<MonitoredTaskDto>(items, result.Count, result.PageNumber, result.PageSize);
        return new GetMonitoredTasksResult(paged);
    }

    private static string Invert(string dir) =>
        string.Equals(dir, "ASC", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
}
