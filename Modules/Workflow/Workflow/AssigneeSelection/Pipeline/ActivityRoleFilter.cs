using Workflow.AssigneeSelection.Teams;

namespace Workflow.AssigneeSelection.Pipeline;

public class ActivityRoleFilter : IAssignmentFilter
{
    private readonly ILogger<ActivityRoleFilter> _logger;

    public int Order => 3;

    public ActivityRoleFilter(ILogger<ActivityRoleFilter> logger)
    {
        _logger = logger;
    }

    public Task<List<TeamMemberInfo>> FilterAsync(
        AssignmentPipelineContext context,
        List<TeamMemberInfo> candidates,
        CancellationToken cancellationToken = default)
    {
        var activityId = context.ActivityContext.ActivityId;

        // Only keep candidates whose ActivityRoles include this activity
        var filtered = candidates.Where(c => c.ActivityRoles.Contains(activityId)).ToList();

        if (filtered.Count < candidates.Count)
        {
            _logger.LogDebug(
                "ActivityRoleFilter: {Before} → {After} candidates for {ActivityId}",
                candidates.Count, filtered.Count, activityId);
        }

        return Task.FromResult(filtered);
    }
}
