using Workflow.AssigneeSelection.Teams;

namespace Workflow.AssigneeSelection.Pipeline;

public class ExclusionFilter : IAssignmentFilter
{
    private readonly ILogger<ExclusionFilter> _logger;

    public int Order => 2;

    public ExclusionFilter(ILogger<ExclusionFilter> logger)
    {
        _logger = logger;
    }

    public Task<List<TeamMemberInfo>> FilterAsync(
        AssignmentPipelineContext context,
        List<TeamMemberInfo> candidates,
        CancellationToken cancellationToken = default)
    {
        var excludeFrom = context.Rules.ExcludeAssigneesFrom;
        if (excludeFrom.Count == 0)
            return Task.FromResult(candidates);

        var excludedUserIds = new HashSet<string>();

        foreach (var sourceActivityId in excludeFrom)
        {
            if (context.PriorAssignees.TryGetValue(sourceActivityId, out var userId))
            {
                excludedUserIds.Add(userId);
            }
        }

        if (excludedUserIds.Count == 0)
            return Task.FromResult(candidates);

        var filtered = candidates.Where(c => !excludedUserIds.Contains(c.UserId)).ToList();

        _logger.LogDebug(
            "ExclusionFilter: Excluded {ExcludedUsers} from {ActivityId}, {Before} → {After} candidates",
            string.Join(",", excludedUserIds), context.ActivityContext.ActivityId, candidates.Count, filtered.Count);

        return Task.FromResult(filtered);
    }
}
