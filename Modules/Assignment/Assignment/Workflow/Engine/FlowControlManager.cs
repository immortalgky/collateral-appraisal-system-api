using Assignment.Workflow.Activities.Core;
using Assignment.Workflow.Engine.Expression;
using Assignment.Workflow.Schema;

namespace Assignment.Workflow.Engine;

/// <summary>
/// Handles workflow flow control logic - Core orchestration responsibility
/// Manages routing decisions, transition evaluation, and workflow flow
/// </summary>
public class FlowControlManager : IFlowControlManager
{
    private readonly ExpressionEvaluator _expressionEvaluator;
    private readonly ILogger<FlowControlManager> _logger;

    public FlowControlManager(ILogger<FlowControlManager> logger)
    {
        _expressionEvaluator = new ExpressionEvaluator();
        _logger = logger;
    }

    public async Task<string?> DetermineNextActivityAsync(
        WorkflowSchema workflowSchema,
        string currentActivityId,
        ActivityResult activityResult,
        Dictionary<string, object> variables,
        CancellationToken cancellationToken = default)
    {
        var currentActivity = workflowSchema.Activities.FirstOrDefault(a => a.Id == currentActivityId);
        var transitions = GetAvailableTransitions(workflowSchema, currentActivityId).ToList();

        if (!transitions.Any())
        {
            _logger.LogDebug("No transitions found for activity {ActivityId}, workflow ending", currentActivityId);
            return null; // No transitions defined, workflow ends
        }

        // Handle activity-specific routing patterns
        var nextActivityId = await HandleActivitySpecificRoutingAsync(currentActivity, activityResult, transitions, variables);
        if (!string.IsNullOrEmpty(nextActivityId))
        {
            return nextActivityId;
        }

        // Handle general conditional routing
        nextActivityId = HandleConditionalRouting(transitions, variables);
        if (!string.IsNullOrEmpty(nextActivityId))
        {
            return nextActivityId;
        }

        // Return default normal transition
        var normalTransition = transitions.FirstOrDefault(t => t.Type == TransitionType.Normal);
        var defaultActivityId = normalTransition?.To;

        _logger.LogDebug("Using default transition from {CurrentActivity} to {NextActivity}", 
            currentActivityId, defaultActivityId ?? "None");

        return defaultActivityId;
    }

    public bool EvaluateTransitionCondition(
        string condition,
        Dictionary<string, object> variables,
        string? decisionValue = null)
    {
        if (string.IsNullOrEmpty(condition))
            return true; // No condition means always true

        // For decision-based transitions, check if condition matches decision
        if (!string.IsNullOrEmpty(decisionValue))
        {
            return string.Equals(condition, decisionValue, StringComparison.OrdinalIgnoreCase);
        }

        // Use expression evaluator for conditional transitions
        try
        {
            return _expressionEvaluator.EvaluateExpression(condition, variables);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to evaluate transition condition '{Condition}'", condition);
            return false;
        }
    }

    public IEnumerable<TransitionDefinition> GetAvailableTransitions(
        WorkflowSchema workflowSchema,
        string activityId)
    {
        return workflowSchema.Transitions
            .Where(t => t.From == activityId)
            .OrderBy(t => t.Type == TransitionType.Normal ? 1 : 0); // Prioritize conditional transitions
    }

    public bool ValidateWorkflowTransitions(WorkflowSchema workflowSchema)
    {
        try
        {
            foreach (var transition in workflowSchema.Transitions)
            {
                var fromActivity = workflowSchema.Activities.FirstOrDefault(a => a.Id == transition.From);
                var toActivity = workflowSchema.Activities.FirstOrDefault(a => a.Id == transition.To);

                if (fromActivity == null)
                {
                    _logger.LogWarning("Transition references unknown source activity: {ActivityId}", transition.From);
                    return false;
                }

                if (toActivity == null)
                {
                    _logger.LogWarning("Transition references unknown target activity: {ActivityId}", transition.To);
                    return false;
                }

                // Validate conditional transitions have proper conditions
                if (transition.Type == TransitionType.Conditional && string.IsNullOrEmpty(transition.Condition))
                {
                    _logger.LogWarning("Conditional transition from {From} to {To} has no condition", 
                        transition.From, transition.To);
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating workflow transitions");
            return false;
        }
    }

    public ActivityDefinition GetStartActivity(WorkflowSchema workflowSchema)
    {
        if (workflowSchema?.Activities == null || !workflowSchema.Activities.Any())
        {
            throw new InvalidOperationException("Workflow schema has no activities");
        }

        // Find explicit start activity
        var startActivity = workflowSchema.Activities.FirstOrDefault(a => a.IsStartActivity);
        if (startActivity != null)
        {
            return startActivity;
        }

        // Fallback to first activity
        var firstActivity = workflowSchema.Activities.First();
        _logger.LogWarning("No explicit start activity found in workflow schema, using first activity: {ActivityId}",
            firstActivity.Id);

        return firstActivity;
    }

    /// <summary>
    /// Handle activity-specific routing patterns (IfElse, Switch activities)
    /// </summary>
    private async Task<string?> HandleActivitySpecificRoutingAsync(
        ActivityDefinition? currentActivity,
        ActivityResult activityResult,
        IList<TransitionDefinition> transitions,
        Dictionary<string, object> variables)
    {
        if (currentActivity == null) return null;

        switch (currentActivity.Type)
        {
            case ActivityTypes.IfElseActivity:
                return HandleIfElseRouting(activityResult, transitions, variables);

            case ActivityTypes.SwitchActivity:
                return HandleSwitchRouting(activityResult, transitions, variables);

            default:
                return null; // No special handling needed
        }
    }

    /// <summary>
    /// Handle IfElse activity routing based on result output
    /// </summary>
    private string? HandleIfElseRouting(
        ActivityResult activityResult,
        IList<TransitionDefinition> transitions,
        Dictionary<string, object> variables)
    {
        if (!activityResult.OutputData.TryGetValue("result", out var resultValue))
        {
            return null;
        }

        var tempVariables = new Dictionary<string, object>(variables)
        {
            ["result"] = resultValue
        };

        var matchingTransition = transitions.FirstOrDefault(t =>
            t.Type == TransitionType.Conditional &&
            EvaluateTransitionCondition(t.Condition ?? "", tempVariables));

        if (matchingTransition != null)
        {
            _logger.LogDebug("IfElse routing: result={Result}, selected transition to {NextActivity}", 
                resultValue, matchingTransition.To);
        }

        return matchingTransition?.To;
    }

    /// <summary>
    /// Handle Switch activity routing based on case output
    /// </summary>
    private string? HandleSwitchRouting(
        ActivityResult activityResult,
        IList<TransitionDefinition> transitions,
        Dictionary<string, object> variables)
    {
        if (!activityResult.OutputData.TryGetValue("case", out var caseValue))
        {
            return null;
        }

        var tempVariables = new Dictionary<string, object>(variables)
        {
            ["case"] = caseValue?.ToString() ?? "default"
        };

        var matchingTransition = transitions.FirstOrDefault(t =>
            t.Type == TransitionType.Conditional &&
            EvaluateTransitionCondition(t.Condition ?? "", tempVariables));

        if (matchingTransition != null)
        {
            _logger.LogDebug("Switch routing: case={Case}, selected transition to {NextActivity}", 
                caseValue, matchingTransition.To);
        }

        return matchingTransition?.To;
    }

    /// <summary>
    /// Handle general conditional routing using workflow variables
    /// </summary>
    private string? HandleConditionalRouting(
        IList<TransitionDefinition> transitions,
        Dictionary<string, object> variables)
    {
        foreach (var transition in transitions.Where(t => t.Type == TransitionType.Conditional))
        {
            if (EvaluateTransitionCondition(transition.Condition ?? "", variables))
            {
                _logger.LogDebug("Conditional routing: condition '{Condition}' matched, selected transition to {NextActivity}", 
                    transition.Condition, transition.To);
                return transition.To;
            }
        }

        return null;
    }
}