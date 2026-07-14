using Dapper;
using Shared.Data;
using Shared.Identity;
using Shared.Pagination;

namespace Workflow.Tasks.Features.GetMonitoredPeople;

public class GetMonitoredPeopleQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUserService,
    ITaskMonitorScope taskMonitorScope
) : IQueryHandler<GetMonitoredPeopleQuery, GetMonitoredPeopleResult>
{
    private static readonly HashSet<string> AllowedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "UserName", "DisplayName", "OpenTasks", "AvailableTasks", "TotalTasks"
    };

    public async Task<GetMonitoredPeopleResult> Handle(
        GetMonitoredPeopleQuery query, CancellationToken cancellationToken)
    {
        var currentUser = currentUserService.Username;

        // Aggregate vw_TaskMonitor by assignee. The base WHERE scopes to active
        // (Assigned|InProgress) person-assigned tasks held by users in the
        // supervisor's monitored groups — this is the same supervisor scope
        // used by GetMonitoredTasks.
        var baseSql = """
            SELECT
                AssignedTo                                              AS UserName,
                MAX(AssignedToDisplayName)                              AS DisplayName,
                SUM(CASE WHEN TaskStatus = 'InProgress' THEN 1 ELSE 0 END) AS OpenTasks,
                SUM(CASE WHEN TaskStatus = 'Assigned'   THEN 1 ELSE 0 END) AS AvailableTasks,
                COUNT(*)                                                AS TotalTasks
            FROM workflow.vw_TaskMonitor
            WHERE AssignedTo IN (
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
            """;

        var parameters = new DynamicParameters();
        parameters.Add("SupervisorNormalizedUserName", currentUser?.ToUpperInvariant() ?? "");

        // Additional :TEAM scope: when the user holds only TASK_MONITOR_VIEW:TEAM (not the base
        // permission), confine the aggregate to their own team (internal) or company (external).
        var teamScopeClause = taskMonitorScope.BuildScopeClause("TASK_MONITOR_VIEW", parameters);
        if (teamScopeClause is not null)
            baseSql += " AND " + teamScopeClause;

        var search = query.Filter?.Search;
        if (!string.IsNullOrWhiteSpace(search))
        {
            baseSql += " AND (AssignedTo LIKE '%' + @Search + '%' OR AssignedToDisplayName LIKE '%' + @Search + '%')";
            parameters.Add("Search", search);
        }

        baseSql += " GROUP BY AssignedTo";

        // Wrap as a subquery so QueryPaginatedAsync can apply OFFSET/FETCH cleanly.
        var sql = $"SELECT * FROM ({baseSql}) p";

        var filter = query.Filter;
        var sortField = AllowedSortFields.Contains(filter?.SortBy ?? "") ? filter!.SortBy! : "UserName";
        var sortDir = string.Equals(filter?.SortDir, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        var orderBy = $"{sortField} {sortDir}";

        var result = await connectionFactory.QueryPaginatedAsync<MonitoredPersonDto>(
            sql, orderBy, query.PaginationRequest, parameters);

        return new GetMonitoredPeopleResult(result);
    }
}
