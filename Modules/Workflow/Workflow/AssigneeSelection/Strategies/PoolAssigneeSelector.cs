using Workflow.AssigneeSelection.Core;

namespace Workflow.AssigneeSelection.Strategies;

/// <summary>
/// Assigns tasks to a pool/group instead of a specific person.
/// All users in the group can see the task and optionally claim it.
/// Returns the group name(s) as AssigneeId with AssignedType="2" in metadata.
/// </summary>
/// <remarks>Multi-group emission (e.g. "Group1,Group2") is not supported by <see cref="Workflow.Tasks.Authorization.PoolTaskAccess"/> matching; schemas must emit a single-group <c>assigneeGroup</c>.</remarks>
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
        // Build pool group identifier from UserGroups, scoped to the team only when the pipeline
        // actually resolved one.
        string poolGroups;

        if (context.UserGroups.Count > 0)
        {
            var groups = string.Join(",", context.UserGroups);

            // Scope to a team ONLY when Stage 1 resolved one. Deriving it from CandidatePool would be
            // wrong whenever the pool holds the whole group (no team constraint, or a constraint whose
            // team could not be derived) — the pool is unordered, so an arbitrary member's team would
            // win and hide the task from every other team in the group.
            poolGroups = !string.IsNullOrEmpty(context.TeamId)
                ? $"{groups}:Team_{context.TeamId}"
                : groups;
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
