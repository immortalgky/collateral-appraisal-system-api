using Assignment.Workflow.Activities.Core;

namespace Assignment.Workflow.Actions.Core;

/// <summary>
/// Service responsible for executing workflow actions during activity lifecycle events
/// Manages action registration, validation, and execution
/// </summary>
public interface IWorkflowActionExecutor
{
    /// <summary>
    /// Executes a collection of workflow actions for a specific lifecycle event
    /// </summary>
    /// <param name="context">Activity context</param>
    /// <param name="actionConfigurations">List of action configurations to execute</param>
    /// <param name="lifecycleEvent">The lifecycle event that triggered these actions</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Aggregated results of all action executions</returns>
    Task<ActionBatchExecutionResult> ExecuteActionsAsync(
        ActivityContext context,
        List<WorkflowActionConfiguration> actionConfigurations,
        ActivityLifecycleEvent lifecycleEvent,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Executes a single workflow action
    /// </summary>
    /// <param name="context">Activity context</param>
    /// <param name="actionConfiguration">Action configuration to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the action execution</returns>
    Task<ActionExecutionResult> ExecuteActionAsync(
        ActivityContext context,
        WorkflowActionConfiguration actionConfiguration,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates a collection of action configurations
    /// </summary>
    /// <param name="actionConfigurations">Action configurations to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation results for all actions</returns>
    Task<ActionBatchValidationResult> ValidateActionsAsync(
        List<WorkflowActionConfiguration> actionConfigurations,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all available action types
    /// </summary>
    /// <returns>List of registered action types</returns>
    IEnumerable<string> GetAvailableActionTypes();
    
    /// <summary>
    /// Gets detailed information about a specific action type
    /// </summary>
    /// <param name="actionType">Action type to get information for</param>
    /// <returns>Action type information or null if not found</returns>
    ActionTypeInfo? GetActionTypeInfo(string actionType);
}

/// <summary>
/// Configuration for a workflow action
/// </summary>
public class WorkflowActionConfiguration
{
    /// <summary>
    /// Type of action to execute
    /// </summary>
    public string ActionType { get; init; } = default!;
    
    /// <summary>
    /// Action-specific parameters and configuration
    /// </summary>
    public Dictionary<string, object> Parameters { get; init; } = new();
    
    /// <summary>
    /// Optional condition that must be true for the action to execute
    /// Supports workflow variable expressions
    /// </summary>
    public string? Condition { get; init; }
    
    /// <summary>
    /// Whether to continue executing other actions if this one fails
    /// </summary>
    public bool ContinueOnFailure { get; init; } = true;
    
    /// <summary>
    /// Optional name/identifier for this specific action instance
    /// </summary>
    public string? Name { get; init; }
    
    /// <summary>
    /// Priority order for action execution (lower numbers execute first)
    /// </summary>
    public int Priority { get; init; } = 100;
}

/// <summary>
/// Result of executing multiple workflow actions
/// </summary>
public class ActionBatchExecutionResult
{
    public bool IsSuccess { get; init; }
    public int SuccessfulActions { get; init; }
    public int FailedActions { get; init; }
    public int SkippedActions { get; init; }
    public List<ActionExecutionResult> Results { get; init; } = new();
    public Dictionary<string, object> AggregatedOutputData { get; init; } = new();
    public TimeSpan TotalExecutionDuration { get; init; }
    public string? ErrorSummary { get; init; }
    
    public static ActionBatchExecutionResult FromResults(List<ActionExecutionResult> results, TimeSpan totalDuration)
    {
        var successful = results.Count(r => r.IsSuccess);
        var failed = results.Count(r => !r.IsSuccess);
        
        var aggregatedData = new Dictionary<string, object>();
        foreach (var result in results)
        {
            foreach (var kvp in result.OutputData)
            {
                aggregatedData[kvp.Key] = kvp.Value;
            }
        }
        
        var errorMessages = results
            .Where(r => !r.IsSuccess && !string.IsNullOrEmpty(r.ErrorMessage))
            .Select(r => r.ErrorMessage!)
            .ToList();
        
        return new ActionBatchExecutionResult
        {
            IsSuccess = failed == 0,
            SuccessfulActions = successful,
            FailedActions = failed,
            Results = results,
            AggregatedOutputData = aggregatedData,
            TotalExecutionDuration = totalDuration,
            ErrorSummary = errorMessages.Any() ? string.Join("; ", errorMessages) : null
        };
    }
}

/// <summary>
/// Result of validating multiple workflow actions
/// </summary>
public class ActionBatchValidationResult
{
    public bool IsValid { get; init; }
    public List<ActionValidationResult> Results { get; init; } = new();
    public List<string> AllErrors { get; init; } = new();
    public List<string> AllWarnings { get; init; } = new();
    
    public static ActionBatchValidationResult FromResults(List<ActionValidationResult> results)
    {
        var allErrors = results.SelectMany(r => r.Errors).ToList();
        var allWarnings = results.SelectMany(r => r.Warnings).ToList();
        
        return new ActionBatchValidationResult
        {
            IsValid = results.All(r => r.IsValid),
            Results = results,
            AllErrors = allErrors,
            AllWarnings = allWarnings
        };
    }
}

/// <summary>
/// Information about an action type
/// </summary>
public class ActionTypeInfo
{
    public string ActionType { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string Description { get; init; } = default!;
    public List<ActionParameterInfo> RequiredParameters { get; init; } = new();
    public List<ActionParameterInfo> OptionalParameters { get; init; } = new();
    public List<string> SampleConfigurations { get; init; } = new();
}

/// <summary>
/// Information about an action parameter
/// </summary>
public class ActionParameterInfo
{
    public string Name { get; init; } = default!;
    public string Type { get; init; } = default!;
    public string Description { get; init; } = default!;
    public object? DefaultValue { get; init; }
    public bool IsRequired { get; init; }
}

/// <summary>
/// Activity lifecycle events that can trigger actions
/// </summary>
public enum ActivityLifecycleEvent
{
    OnStart,
    OnComplete,
    OnError,
    OnCancel,
    OnTimeout
}