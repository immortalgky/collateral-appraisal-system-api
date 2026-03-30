using System.Text.Json;
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

        // Read assigneeRole from schema properties to match against candidate roles
        var roleName = "";
        if (context.ActivityContext.Properties?.TryGetValue("assigneeRole", out var role) == true && role is not null)
        {
            roleName = role is JsonElement je ? je.GetString() ?? "" : role.ToString() ?? "";
        }

        if (string.IsNullOrEmpty(roleName))
            return Task.FromResult(candidates);

        // Only keep candidates whose roles include the required role
        var filtered = candidates.Where(c => c.ActivityRoles.Contains(roleName)).ToList();

        if (filtered.Count < candidates.Count)
        {
            _logger.LogDebug(
                "ActivityRoleFilter: {Before} → {After} candidates for {ActivityId} (role: {RoleName})",
                candidates.Count, filtered.Count, activityId, roleName);
        }

        return Task.FromResult(filtered);
    }
}
