using Dapper;
using Shared.Data;
using Shared.Identity;
using Shared.Pagination;
using Workflow.AssigneeSelection.Teams;
using Workflow.Services.Groups;
using Workflow.Tasks.Authorization;
using Workflow.Tasks.Features.Shared;

namespace Workflow.Tasks.Features.GetPoolTasks;

public class GetPoolTasksQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUserService,
    IUserGroupService userGroupService,
    ITeamService teamService
) : IQueryHandler<GetPoolTasksQuery, GetPoolTasksResult>
{
    public async Task<GetPoolTasksResult> Handle(
        GetPoolTasksQuery query,
        CancellationToken cancellationToken)
    {
        var username = currentUserService.Username;
        if (string.IsNullOrEmpty(username))
            return new GetPoolTasksResult(new PaginatedResult<PoolTaskDto>([], 0, 0, 10));

        // Get user's groups to match against pool assignments
        var userGroups = await userGroupService.GetGroupsForUserAsync(username, cancellationToken);
        if (userGroups.Count == 0)
            return new GetPoolTasksResult(new PaginatedResult<PoolTaskDto>([], 0, 0, 10));

        var team = await teamService.GetTeamForUserAsync(username, cancellationToken);

        var clause = PoolTaskAccess.BuildSqlClause(userGroups, team?.TeamId, currentUserService.CompanyId, username);
        if (clause is null)
            return new GetPoolTasksResult(new PaginatedResult<PoolTaskDto>([], 0, 0, 10));

        var sql = "SELECT * FROM workflow.vw_TaskList";
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        conditions.Add("AssignedType = '2'");
        conditions.Add(clause.Sql);
        foreach (var (k, v) in clause.Parameters)
            parameters.Add(k, v);

        var filter = query.Filter;
        TaskListFilterBuilder.ApplyFilters(filter, conditions, parameters);

        if (conditions.Count > 0)
            sql += " WHERE " + string.Join(" AND ", conditions);

        var orderBy = TaskListFilterBuilder.ResolveOrderBy(filter);

        var result = await connectionFactory.QueryPaginatedAsync<PoolTaskDto>(
            sql,
            orderBy,
            query.PaginationRequest,
            parameters);

        return new GetPoolTasksResult(result);
    }
}
