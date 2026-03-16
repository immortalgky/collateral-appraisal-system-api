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

        if (!context.Rules.TeamConstrained)
        {
            // Not team-constrained: load all members for this activity role if pool is empty
            if (candidates.Count == 0)
            {
                candidates = await _teamService.GetAllMembersForActivityAsync(activityId, cancellationToken);
                _logger.LogDebug("TeamFilter: Loaded {Count} members for {ActivityId} (no team constraint)", candidates.Count, activityId);
            }
            return candidates;
        }

        // Team-constrained: scope to the workflow's team
        var teamId = context.TeamId;

        if (string.IsNullOrEmpty(teamId))
        {
            // No team set yet — load all members so first assignment can establish the team
            candidates = await _teamService.GetAllMembersForActivityAsync(activityId, cancellationToken);
            _logger.LogDebug("TeamFilter: No TeamId set, loaded {Count} members for {ActivityId}", candidates.Count, activityId);
            return candidates;
        }

        candidates = await _teamService.GetTeamMembersForActivityAsync(teamId, activityId, cancellationToken);
        _logger.LogDebug("TeamFilter: Scoped to team {TeamId}, {Count} members for {ActivityId}", teamId, candidates.Count, activityId);

        return candidates;
    }
}
