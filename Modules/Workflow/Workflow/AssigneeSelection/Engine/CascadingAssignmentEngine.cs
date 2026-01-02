using Workflow.Workflow.Models;

namespace Workflow.AssigneeSelection.Engine;

/// <summary>
/// Implementation of cascading assignment engine that tries multiple strategies in sequence
/// </summary>
public class CascadingAssignmentEngine : ICascadingAssignmentEngine
{
    private readonly IAssigneeSelectorFactory _selectorFactory;
    private readonly WorkflowDbContext _context;
    private readonly ILogger<CascadingAssignmentEngine> _logger;

    public CascadingAssignmentEngine(
        IAssigneeSelectorFactory selectorFactory,
        WorkflowDbContext context,
        ILogger<CascadingAssignmentEngine> logger)
    {
        _selectorFactory = selectorFactory;
        _context = context;
        _logger = logger;
    }

    public async Task<AssigneeSelectionResult> ExecuteAsync(
        AssignmentContext context,
        CancellationToken cancellationToken = default)
    {
        if (!context.AssignmentStrategies.Any())
        {
            return AssigneeSelectionResult.Failure("No assignment strategies provided");
        }

        var attemptedStrategies = new List<string>();
        var failureReasons = new List<string>();

        foreach (var strategyName in context.AssignmentStrategies)
        {
            try
            {
                attemptedStrategies.Add(strategyName);
                
                // Parse strategy name to enum
                var strategy = AssignmentStrategyExtensions.FromString(strategyName);
                
                // Get the appropriate selector
                var selector = _selectorFactory.GetSelector(strategy);
                
                _logger.LogInformation("Attempting assignment strategy {Strategy} for activity {ActivityName}",
                    strategyName, context.ActivityName);

                // Try the strategy
                var result = await selector.SelectAssigneeAsync(context, cancellationToken);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("Assignment successful using strategy {Strategy} for activity {ActivityName}. Assignee: {Assignee}",
                        strategyName, context.ActivityName, result.AssigneeId ?? "Group Assignment");

                    // Add cascading metadata
                    result.Metadata ??= new Dictionary<string, object>();
                    result.Metadata["CascadingStrategies"] = attemptedStrategies;
                    result.Metadata["SuccessfulStrategy"] = strategyName;
                    result.Metadata["StrategyPosition"] = attemptedStrategies.Count;

                    return result;
                }
                else
                {
                    failureReasons.Add($"{strategyName}: {result.ErrorMessage}");
                    _logger.LogWarning("Assignment strategy {Strategy} failed for activity {ActivityName}: {ErrorMessage}",
                        strategyName, context.ActivityName, result.ErrorMessage);
                }
            }
            catch (ArgumentException ex)
            {
                failureReasons.Add($"{strategyName}: Invalid strategy name");
                _logger.LogError(ex, "Invalid assignment strategy {Strategy} for activity {ActivityName}",
                    strategyName, context.ActivityName);
            }
            catch (Exception ex)
            {
                failureReasons.Add($"{strategyName}: {ex.Message}");
                _logger.LogError(ex, "Error executing assignment strategy {Strategy} for activity {ActivityName}",
                    strategyName, context.ActivityName);
            }
        }

        // All strategies failed
        var combinedErrorMessage = $"All assignment strategies failed. Attempted: {string.Join(", ", attemptedStrategies)}. " +
                                 $"Failures: {string.Join("; ", failureReasons)}";

        _logger.LogError("All assignment strategies failed for activity {ActivityName}: {ErrorMessage}",
            context.ActivityName, combinedErrorMessage);

        var failureResult = AssigneeSelectionResult.Failure(combinedErrorMessage);
        failureResult.Metadata = new Dictionary<string, object>
        {
            ["AttemptedStrategies"] = attemptedStrategies,
            ["FailureReasons"] = failureReasons,
            ["CascadingFailed"] = true
        };
        
        return failureResult;
    }

    public async Task<bool> IsRouteBackScenarioAsync(
        Guid workflowInstanceId,
        string activityId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if this activity has been completed before in the same workflow instance
            var previousExecution = await _context.WorkflowActivityExecutions
                .Where(ae => ae.WorkflowInstanceId == workflowInstanceId
                           && ae.ActivityId == activityId
                           && ae.Status == ActivityExecutionStatus.Completed)
                .AnyAsync(cancellationToken);

            _logger.LogDebug("Route-back check for workflow {WorkflowInstanceId}, activity {ActivityId}: {IsRouteBack}",
                workflowInstanceId, activityId, previousExecution);

            return previousExecution;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking route-back scenario for workflow {WorkflowInstanceId}, activity {ActivityId}",
                workflowInstanceId, activityId);
            
            // Default to false if we can't determine
            return false;
        }
    }
}