using Appraisal.Contracts.Services;
using Shared.Identity;
using Workflow.Tasks.Authorization;
using WorkflowGroupService = global::Workflow.Services.Groups.IUserGroupService;
using WorkflowTeamService = global::Workflow.AssigneeSelection.Teams.ITeamService;

namespace Workflow.Tasks.Services;

/// <summary>
/// Implements IPoolTaskClauseService for the Appraisal module.
/// Resolves the caller's groups + team then delegates to PoolTaskAccess.BuildSqlClause.
/// </summary>
public class PoolTaskClauseService(
    ICurrentUserService currentUser,
    WorkflowGroupService userGroupService,
    WorkflowTeamService teamService)
    : IPoolTaskClauseService
{
    public async Task<PoolTaskClause?> BuildClauseForCurrentUserAsync(
        CancellationToken cancellationToken = default)
    {
        var username = currentUser.Username;
        if (string.IsNullOrEmpty(username))
            return null;

        var userGroups = await userGroupService.GetGroupsForUserAsync(username, cancellationToken);
        var team       = await teamService.GetTeamForUserAsync(username, cancellationToken);

        var clause = PoolTaskAccess.BuildSqlClause(userGroups, team?.TeamId, currentUser.CompanyId, username);
        if (clause is null)
            return null;

        return new PoolTaskClause(clause.Sql, clause.Parameters);
    }
}
