using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Schema;

namespace Workflow.Workflow.Engine;

/// <summary>
/// Handles workflow flow control logic including routing decisions and transition evaluation
/// Core orchestration responsibility - determines workflow routing
/// </summary>
public interface IFlowControlManager
{
    /// <summary>
    /// Determines the next activity based on current activity result and workflow schema
    /// </summary>
    Task<string?> DetermineNextActivityAsync(
        WorkflowSchema workflowSchema,
        string currentActivityId,
        ActivityResult activityResult,
        Dictionary<string, object> variables,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates a transition condition against workflow variables
    /// </summary>
    bool EvaluateTransitionCondition(
        string condition,
        Dictionary<string, object> variables,
        string? decisionValue = null);

    /// <summary>
    /// Gets available transitions from a specific activity
    /// </summary>
    IEnumerable<TransitionDefinition> GetAvailableTransitions(
        WorkflowSchema workflowSchema,
        string activityId);

    /// <summary>
    /// Validates that workflow transitions are properly configured
    /// </summary>
    bool ValidateWorkflowTransitions(WorkflowSchema workflowSchema);
    
    /// <summary>
    /// Gets the initial activity for a workflow schema
    /// </summary>
    ActivityDefinition GetStartActivity(WorkflowSchema workflowSchema);
}