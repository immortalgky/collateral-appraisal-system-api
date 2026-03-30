using System.Text.Json;
using Workflow.AssigneeSelection.Teams;

namespace Workflow.AssigneeSelection.Pipeline;

public class TeamFilter : IAssignmentFilter
{
    private readonly ITeamService _teamService;
    private readonly ILogger<TeamFilter> _logger;

    public int Order => 1;

    public TeamFilter(ITeamService teamService, ILogger<TeamFilter> logger)
    {
        _teamService = teamService;
        _logger = logger;
    }

    public async Task<List<TeamMemberInfo>> FilterAsync(
        AssignmentPipelineContext context,
        List<TeamMemberInfo> candidates,
        CancellationToken cancellationToken = default)
    {
        var activityId = context.ActivityContext.ActivityId;

        // Read the role name from the schema's assigneeRole property
        // Values from JSON deserialization may be JsonElement, so handle both types
        var roleName = "";
        if (context.ActivityContext.Properties?.TryGetValue("assigneeRole", out var role) == true && role is not null)
        {
            roleName = role is JsonElement je ? je.GetString() ?? "" : role.ToString() ?? "";
        }

        if (string.IsNullOrEmpty(roleName))
        {
            _logger.LogWarning("No assigneeRole in properties for activity {ActivityId}", activityId);
            return candidates;
        }

        if (!context.Rules.TeamConstrained)
        {
            // Not team-constrained: load all members for this role if pool is empty
            if (candidates.Count == 0)
            {
                candidates = await _teamService.GetAllMembersForActivityAsync(roleName, cancellationToken);
                _logger.LogDebug("TeamFilter: Loaded {Count} members for {ActivityId} role {RoleName} (no team constraint)", candidates.Count, activityId, roleName);
            }
            return candidates;
        }

        // Team-constrained: scope to the workflow's team
        var teamId = context.TeamId;

        if (string.IsNullOrEmpty(teamId))
        {
            // No team set yet -- load all members so first assignment can establish the team
            candidates = await _teamService.GetAllMembersForActivityAsync(roleName, cancellationToken);
            _logger.LogDebug("TeamFilter: No TeamId set, loaded {Count} members for {ActivityId} role {RoleName}", candidates.Count, activityId, roleName);
            return candidates;
        }

        candidates = await _teamService.GetTeamMembersForActivityAsync(teamId, roleName, cancellationToken);
        _logger.LogDebug("TeamFilter: Scoped to team {TeamId}, {Count} members for {ActivityId} role {RoleName}", teamId, candidates.Count, activityId, roleName);

        return candidates;
    }
}
