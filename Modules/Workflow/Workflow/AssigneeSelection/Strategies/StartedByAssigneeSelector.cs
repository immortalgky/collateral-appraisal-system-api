using Workflow.AssigneeSelection.Core;

namespace Workflow.AssigneeSelection.Strategies;

/// <summary>
/// Assigns tasks to the user who originally started the workflow instance (route-back to requestor)
/// </summary>
public class StartedByAssigneeSelector : IAssigneeSelector
{
    private readonly ILogger<StartedByAssigneeSelector> _logger;

    public StartedByAssigneeSelector(ILogger<StartedByAssigneeSelector> logger)
    {
        _logger = logger;
    }

    public Task<AssigneeSelectionResult> SelectAssigneeAsync(
        AssignmentContext context,
        CancellationToken cancellationToken = default)
    {
        var startedBy = context.StartedBy;

        if (string.IsNullOrWhiteSpace(startedBy))
        {
            _logger.LogWarning(
                "StartedBy selector failed for activity {ActivityName}: StartedBy is not available",
                context.ActivityName);

            return Task.FromResult(
                AssigneeSelectionResult.Failure("StartedBy strategy requires a non-empty StartedBy value on the context"));
        }

        _logger.LogInformation(
            "StartedBy selector assigned user {UserId} for activity {ActivityName}",
            startedBy, context.ActivityName);

        return Task.FromResult(
            AssigneeSelectionResult.Success(startedBy, new Dictionary<string, object>
            {
                ["SelectionStrategy"] = "StartedBy",
                ["WorkflowInitiator"] = startedBy
            }));
    }
}
