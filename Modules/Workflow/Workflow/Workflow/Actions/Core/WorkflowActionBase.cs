using Workflow.Workflow.Activities.Core;

namespace Workflow.Workflow.Actions.Core;

/// <summary>
/// Base class for workflow actions providing common functionality
/// </summary>
public abstract class WorkflowActionBase : IWorkflowAction
{
    protected readonly ILogger Logger;

    protected WorkflowActionBase(ILogger logger)
    {
        Logger = logger;
    }

    public abstract string ActionType { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }

    public async Task<ActionExecutionResult> ExecuteAsync(
        ActivityContext context,
        Dictionary<string, object> actionParameters,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            Logger.LogInformation("Executing workflow action '{ActionType}' for activity {ActivityId}",
                ActionType, context.ActivityId);

            // Validate parameters before execution
            var validationResult = await ValidateAsync(actionParameters, cancellationToken);
            if (!validationResult.IsValid)
            {
                var errorMessage = $"Action validation failed: {string.Join(", ", validationResult.Errors)}";
                Logger.LogError("Action validation failed for '{ActionType}': {Errors}",
                    ActionType, string.Join(", ", validationResult.Errors));
                
                return ActionExecutionResult.Failed(errorMessage, executionDuration: stopwatch.Elapsed);
            }

            // Log any validation warnings
            if (validationResult.Warnings.Any())
            {
                Logger.LogWarning("Action validation warnings for '{ActionType}': {Warnings}",
                    ActionType, string.Join(", ", validationResult.Warnings));
            }

            // Execute the action
            var result = await ExecuteActionAsync(context, actionParameters, cancellationToken);
            
            stopwatch.Stop();
            
            if (result.IsSuccess)
            {
                Logger.LogInformation("Successfully executed action '{ActionType}' for activity {ActivityId} in {Duration}ms",
                    ActionType, context.ActivityId, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                Logger.LogError("Action '{ActionType}' failed for activity {ActivityId}: {Error}",
                    ActionType, context.ActivityId, result.ErrorMessage);
            }

            // Update execution duration if not already set
            if (result.ExecutionDuration == default)
            {
                return new ActionExecutionResult
                {
                    IsSuccess = result.IsSuccess,
                    ErrorMessage = result.ErrorMessage,
                    ResultMessage = result.ResultMessage,
                    OutputData = result.OutputData,
                    Metadata = result.Metadata,
                    ExecutionDuration = stopwatch.Elapsed
                };
            }

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Unexpected error executing action '{ActionType}' for activity {ActivityId}",
                ActionType, context.ActivityId);
            
            return ActionExecutionResult.Failed(
                $"Unexpected error in action '{ActionType}': {ex.Message}",
                executionDuration: stopwatch.Elapsed);
        }
    }

    public virtual Task<ActionValidationResult> ValidateAsync(
        Dictionary<string, object> actionParameters,
        CancellationToken cancellationToken = default)
    {
        // Default implementation - subclasses can override for specific validation
        return Task.FromResult(ActionValidationResult.Valid());
    }

    /// <summary>
    /// Executes the specific action logic - implemented by subclasses
    /// </summary>
    /// <param name="context">Activity context</param>
    /// <param name="actionParameters">Action parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Action execution result</returns>
    protected abstract Task<ActionExecutionResult> ExecuteActionAsync(
        ActivityContext context,
        Dictionary<string, object> actionParameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a parameter value with type conversion and default value support
    /// </summary>
    /// <typeparam name="T">Parameter type</typeparam>
    /// <param name="parameters">Parameters dictionary</param>
    /// <param name="key">Parameter key</param>
    /// <param name="defaultValue">Default value if parameter is not found</param>
    /// <returns>Parameter value or default</returns>
    protected T GetParameter<T>(Dictionary<string, object> parameters, string key, T defaultValue = default!)
    {
        if (!parameters.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        try
        {
            if (value is T directValue)
            {
                return directValue;
            }

            // Attempt type conversion
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Failed to convert parameter '{Key}' to type {Type}: {Error}. Using default value.",
                key, typeof(T).Name, ex.Message);
            return defaultValue;
        }
    }

    /// <summary>
    /// Validates that a required parameter is present and not null/empty
    /// </summary>
    /// <param name="parameters">Parameters dictionary</param>
    /// <param name="key">Parameter key</param>
    /// <param name="errors">Error list to add validation errors to</param>
    protected void ValidateRequiredParameter(Dictionary<string, object> parameters, string key, List<string> errors)
    {
        if (!parameters.ContainsKey(key))
        {
            errors.Add($"Required parameter '{key}' is missing");
            return;
        }

        var value = parameters[key];
        if (value == null)
        {
            errors.Add($"Required parameter '{key}' cannot be null");
            return;
        }

        if (value is string stringValue && string.IsNullOrWhiteSpace(stringValue))
        {
            errors.Add($"Required parameter '{key}' cannot be empty");
        }
    }

    /// <summary>
    /// Resolves workflow variable expressions in a parameter value
    /// Supports patterns like ${variableName} or ${workflow.status}
    /// </summary>
    /// <param name="value">Value that may contain variable expressions</param>
    /// <param name="context">Activity context with workflow variables</param>
    /// <returns>Resolved value</returns>
    protected string ResolveVariableExpressions(string value, ActivityContext context)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var resolvedValue = value;
        
        // Simple variable resolution - can be enhanced with more sophisticated expression engine
        var variables = context.Variables.Concat(context.Properties)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        
        foreach (var variable in variables)
        {
            var placeholder = "${" + variable.Key + "}";
            if (resolvedValue.Contains(placeholder))
            {
                resolvedValue = resolvedValue.Replace(placeholder, variable.Value?.ToString() ?? "");
            }
        }

        // Add workflow instance properties
        resolvedValue = resolvedValue.Replace("${workflow.instanceId}", context.WorkflowInstanceId.ToString());
        resolvedValue = resolvedValue.Replace("${workflow.activityId}", context.ActivityId);
        resolvedValue = resolvedValue.Replace("${workflow.assignee}", context.CurrentAssignee ?? "");

        return resolvedValue;
    }
}