using Dapper;
using Shared.Data;
using Shared.Identity;
using Workflow.AssigneeSelection.Teams;
using Workflow.Services.Groups;
using Workflow.Tasks.Authorization;

namespace Workflow.Tasks.Features.GetTaskCounts;

public class GetTaskCountsQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUserService,
    IUserGroupService userGroupService,
    ITeamService teamService
) : IQueryHandler<GetTaskCountsQuery, GetTaskCountsResult>
{
    public async Task<GetTaskCountsResult> Handle(
        GetTaskCountsQuery query,
        CancellationToken cancellationToken)
    {
        var username = currentUserService.Username;
        if (string.IsNullOrEmpty(username))
            return new GetTaskCountsResult([]);

        var parameters = new DynamicParameters();
        parameters.Add("AssigneeUserId", username);

        var poolClauseSql = "1 = 0";
        var userGroups = await userGroupService.GetGroupsForUserAsync(username, cancellationToken);
        if (userGroups.Count > 0)
        {
            var team = await teamService.GetTeamForUserAsync(username, cancellationToken);
            var poolClause = PoolTaskAccess.BuildSqlClause(userGroups, team?.TeamId, currentUserService.CompanyId, username);
            if (poolClause is not null)
            {
                poolClauseSql = poolClause.Sql;
                foreach (var (k, v) in poolClause.Parameters)
                    parameters.Add(k, v);
            }
        }

        var sql = $@"
SELECT
    ActivityId,
    SUM(CASE WHEN AssignedType = '1' AND AssigneeUserId = @AssigneeUserId THEN 1 ELSE 0 END) AS MyCount,
    SUM(CASE WHEN AssignedType = '2' AND {poolClauseSql} THEN 1 ELSE 0 END) AS PoolCount
FROM workflow.vw_TaskList
WHERE ActivityId IS NOT NULL
  AND (
    (AssignedType = '1' AND AssigneeUserId = @AssigneeUserId)
    OR (AssignedType = '2' AND {poolClauseSql})
  )
GROUP BY ActivityId
HAVING
    SUM(CASE WHEN AssignedType = '1' AND AssigneeUserId = @AssigneeUserId THEN 1 ELSE 0 END) > 0
    OR SUM(CASE WHEN AssignedType = '2' AND {poolClauseSql} THEN 1 ELSE 0 END) > 0;
";

        var connection = connectionFactory.GetOpenConnection();
        var rows = await connection.QueryAsync<ActivityTaskCountDto>(sql, parameters);
        return new GetTaskCountsResult(rows.ToList());
    }
}
