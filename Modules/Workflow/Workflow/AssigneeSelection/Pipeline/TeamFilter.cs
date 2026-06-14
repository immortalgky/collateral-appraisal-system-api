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

        // Group precedence (RuntimeOverride > DB config > JSON definition) is resolved once in
        // AssignmentContextBuilder; read the single shared value so Stage 2 and Stage 3 cannot disagree.
        var groupName = context.ResolvedAssigneeGroup ?? "";

        if (string.IsNullOrEmpty(groupName))
        {
            _logger.LogWarning("No assigneeGroup in properties for activity {ActivityId}", activityId);
            return candidates;
        }

        if (!context.Rules.TeamConstrained)
        {
            // Not team-constrained: load all members for this group if pool is empty
            if (candidates.Count == 0)
            {
                candidates = await _teamService.GetAllMembersForActivityAsync(groupName, cancellationToken);
                _logger.LogDebug("TeamFilter: Loaded {Count} members for {ActivityId} group {GroupName} (no team constraint)", candidates.Count, activityId, groupName);
            }
            return candidates;
        }

        // Team-constrained: scope to the workflow's team
        var teamId = context.TeamId;

        if (string.IsNullOrEmpty(teamId))
        {
            // No team set yet -- load all members so first assignment can establish the team
            candidates = await _teamService.GetAllMembersForActivityAsync(groupName, cancellationToken);
            _logger.LogDebug("TeamFilter: No TeamId set, loaded {Count} members for {ActivityId} group {GroupName}", candidates.Count, activityId, groupName);
            return candidates;
        }

        candidates = await _teamService.GetTeamMembersForActivityAsync(teamId, groupName, cancellationToken);
        _logger.LogDebug("TeamFilter: Scoped to team {TeamId}, {Count} members for {ActivityId} group {GroupName}", teamId, candidates.Count, activityId, groupName);

        return candidates;
    }
}
