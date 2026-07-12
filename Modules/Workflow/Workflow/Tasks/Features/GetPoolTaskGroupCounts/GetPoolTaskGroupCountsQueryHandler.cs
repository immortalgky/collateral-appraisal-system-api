using System.Data;
using Dapper;
using Shared.Data;
using Shared.Identity;
using Workflow.AssigneeSelection.Teams;
using Workflow.Services.Groups;
using Workflow.Tasks.Authorization;
using Workflow.Tasks.Features.GetMyTaskGroupCounts;
using Workflow.Tasks.Features.Shared;

namespace Workflow.Tasks.Features.GetPoolTaskGroupCounts;

public class GetPoolTaskGroupCountsQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUserService,
    IUserGroupService userGroupService,
    ITeamService teamService
) : IQueryHandler<GetPoolTaskGroupCountsQuery, GetPoolTaskGroupCountsResult>
{
    private static readonly HashSet<string> AllowedGroupBy = new(StringComparer.OrdinalIgnoreCase)
    {
        "status", "priority", "purpose", "activity", "slaStatus"
    };

    public async Task<GetPoolTaskGroupCountsResult> Handle(
        GetPoolTaskGroupCountsQuery query,
        CancellationToken cancellationToken)
    {
        if (!AllowedGroupBy.Contains(query.GroupBy))
            throw new BadRequestException(
                $"Invalid groupBy '{query.GroupBy}'. Allowed values: status, priority, purpose, activity, slaStatus.");

        var username = currentUserService.Username;
        if (string.IsNullOrEmpty(username))
            return new GetPoolTaskGroupCountsResult([]);

        // Get user's groups to match against pool assignments
        var userGroups = await userGroupService.GetGroupsForUserAsync(username, cancellationToken);
        if (userGroups.Count == 0)
            return new GetPoolTaskGroupCountsResult([]);

        var team = await teamService.GetTeamForUserAsync(username, cancellationToken);

        var (assignees, companyGate) =
            PoolTaskAccess.BuildProcAccess(userGroups, team?.TeamId, currentUserService.CompanyId, username);
        if (assignees.Count == 0)
            return new GetPoolTaskGroupCountsResult([]);

        var parameters = TaskGroupCountsProcParams.Build(
            assignedType: "2",
            assignees: assignees,
            companyGate: companyGate,
            callerCompanyId: currentUserService.CompanyId,
            filter: query.Filter,
            groupBy: query.GroupBy);

        var connection = connectionFactory.GetOpenConnection();
        var rows = await connection.QueryAsync<TaskGroupCountDto>(new CommandDefinition(
            "workflow.sp_GetTaskGroupCounts", parameters,
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));

        return new GetPoolTaskGroupCountsResult(rows.ToList());
    }
}
