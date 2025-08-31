using Assignment.Workflow.Activities.Core;

namespace Assignment.Workflow.Actions.Core;

/// <summary>
/// Base interface for all workflow actions that can be executed during activity lifecycle events
/// Actions represent side effects and business operations that occur during workflow execution
/// </summary>
public interface IWorkflowAction
{
    /// <summary>
    /// Unique identifier for this action type
    /// </summary>
    string ActionType { get; }
    
    /// <summary>
    /// Human-readable name for this action
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Description of what this action does
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// Executes the workflow action with the provided context and parameters
    /// </summary>
    /// <param name="context">Activity context providing workflow and activity information</param>
    /// <param name="actionParameters">Action-specific parameters and configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the action execution</returns>
    Task<ActionExecutionResult> ExecuteAsync(
        ActivityContext context,
        Dictionary<string, object> actionParameters,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates that the action parameters are correct and the action can be executed
    /// </summary>
    /// <param name="actionParameters">Action-specific parameters to validate</param>
    /// <returns>Validation result indicating if the action is properly configured</returns>
    Task<ActionValidationResult> ValidateAsync(
        Dictionary<string, object> actionParameters,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a workflow action execution
/// </summary>
public class ActionExecutionResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public Dictionary<string, object> OutputData { get; init; } = new();
    public Dictionary<string, object> Metadata { get; init; } = new();
    public TimeSpan ExecutionDuration { get; init; }
    public string? ResultMessage { get; init; }
    
    public static ActionExecutionResult Success(
        string? resultMessage = null,
        Dictionary<string, object>? outputData = null,
        Dictionary<string, object>? metadata = null,
        TimeSpan executionDuration = default)
        => new()
        {
            IsSuccess = true,
            ResultMessage = resultMessage,
            OutputData = outputData ?? new Dictionary<string, object>(),
            Metadata = metadata ?? new Dictionary<string, object>(),
            ExecutionDuration = executionDuration
        };
    
    public static ActionExecutionResult Failed(
        string errorMessage,
        Dictionary<string, object>? outputData = null,
        Dictionary<string, object>? metadata = null,
        TimeSpan executionDuration = default)
        => new()
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            OutputData = outputData ?? new Dictionary<string, object>(),
            Metadata = metadata ?? new Dictionary<string, object>(),
            ExecutionDuration = executionDuration
        };
}

/// <summary>
/// Result of workflow action validation
/// </summary>
public class ActionValidationResult
{
    public bool IsValid { get; init; }
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public Dictionary<string, object> ValidationMetadata { get; init; } = new();
    
    public static ActionValidationResult Valid(
        List<string>? warnings = null,
        Dictionary<string, object>? metadata = null)
        => new()
        {
            IsValid = true,
            Warnings = warnings ?? new List<string>(),
            ValidationMetadata = metadata ?? new Dictionary<string, object>()
        };
    
    public static ActionValidationResult Invalid(
        List<string> errors,
        List<string>? warnings = null,
        Dictionary<string, object>? metadata = null)
        => new()
        {
            IsValid = false,
            Errors = errors,
            Warnings = warnings ?? new List<string>(),
            ValidationMetadata = metadata ?? new Dictionary<string, object>()
        };
    
    public static ActionValidationResult Invalid(
        string error,
        List<string>? warnings = null,
        Dictionary<string, object>? metadata = null)
        => Invalid(new List<string> { error }, warnings, metadata);
}