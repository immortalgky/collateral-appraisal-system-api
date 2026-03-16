using System.Security.Cryptography;
using Workflow.AssigneeSelection.Core;

namespace Workflow.AssigneeSelection.Strategies;

/// <summary>
/// Selects an assignee from the pipeline's pre-filtered CandidatePool.
/// Falls back to random selection when no other ordering criteria is available.
/// </summary>
public class TeamConstrainedAssigneeSelector : IAssigneeSelector
{
    private readonly ILogger<TeamConstrainedAssigneeSelector> _logger;

    public TeamConstrainedAssigneeSelector(ILogger<TeamConstrainedAssigneeSelector> logger)
    {
        _logger = logger;
    }

    public Task<AssigneeSelectionResult> SelectAssigneeAsync(AssignmentContext context, CancellationToken cancellationToken = default)
    {
        var pool = context.CandidatePool;

        if (pool is null || pool.Count == 0)
        {
            return Task.FromResult(AssigneeSelectionResult.Failure("No candidates in the pre-filtered pool"));
        }

        // Pick randomly from the filtered pool
        var index = RandomNumberGenerator.GetInt32(pool.Count);
        var selected = pool[index];

        _logger.LogInformation(
            "TeamConstrained selected {UserId} ({DisplayName}) from pool of {Count} for {Activity}",
            selected.UserId, selected.DisplayName, pool.Count, context.ActivityName);

        return Task.FromResult(AssigneeSelectionResult.Success(
            selected.UserId,
            new Dictionary<string, object>
            {
                ["SelectionStrategy"] = "TeamConstrained",
                ["PoolSize"] = pool.Count,
                ["SelectedUser"] = selected.DisplayName,
                ["TeamId"] = selected.TeamId
            }));
    }
}
