using Workflow.AssigneeSelection.Core;

namespace Workflow.AssigneeSelection.Strategies;

/// <summary>
/// Assigns tasks to a pool/group instead of a specific person.
/// All users in the group can see the task and optionally claim it.
/// Returns the group name(s) as AssigneeId with AssignedType="2" in metadata.
/// </summary>
public class PoolAssigneeSelector : IAssigneeSelector
{
    private readonly ILogger<PoolAssigneeSelector> _logger;

    public PoolAssigneeSelector(ILogger<PoolAssigneeSelector> logger)
    {
        _logger = logger;
    }

    public Task<AssigneeSelectionResult> SelectAssigneeAsync(
        AssignmentContext context,
        CancellationToken cancellationToken = default)
    {
        // Build pool group identifier from UserGroups or CandidatePool
        string poolGroups;

        if (context.CandidatePool is { Count: > 0 })
        {
            // Team-constrained: use team ID from candidate pool for scoped group
            var teamId = context.CandidatePool.First().TeamId;
            var groups = context.UserGroups.Count > 0
                ? string.Join(",", context.UserGroups)
                : "Default";
            poolGroups = !string.IsNullOrEmpty(teamId)
                ? $"{groups}:Team_{teamId}"
                : groups;
        }
        else if (context.UserGroups.Count > 0)
        {
            poolGroups = string.Join(",", context.UserGroups);
        }
        else
        {
            _logger.LogWarning(
                "Pool selector failed for activity {ActivityName}: no UserGroups configured",
                context.ActivityName);

            return Task.FromResult(
                AssigneeSelectionResult.Failure("Pool strategy requires at least one UserGroup"));
        }

        _logger.LogInformation(
            "Pool selector assigned to pool '{PoolGroups}' for activity {ActivityName}",
            poolGroups, context.ActivityName);

        return Task.FromResult(
            AssigneeSelectionResult.Success(poolGroups, new Dictionary<string, object>
            {
                ["SelectionStrategy"] = "Pool",
                ["AssignedType"] = "2",
                ["PoolGroups"] = poolGroups
            }));
    }
}
