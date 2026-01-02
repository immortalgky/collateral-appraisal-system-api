namespace Workflow.Workflow.Activities.Core;

/// <summary>
/// Utility class for creating consistent error data across workflow activities
/// Eliminates duplicate error dictionary creation patterns
/// </summary>
public static class ActivityErrorBuilder
{
    /// <summary>
    /// Creates standard error data for validation failures
    /// </summary>
    public static Dictionary<string, object> CreateValidationError(
        string activityId, 
        string message, 
        string? details = null)
    {
        var errorData = new Dictionary<string, object>
        {
            ["error"] = "Validation failed",
            ["errorMessage"] = message,
            ["activityId"] = activityId,
            ["errorType"] = "ValidationError"
        };

        if (!string.IsNullOrEmpty(details))
        {
            errorData["details"] = details;
        }

        return errorData;
    }

    /// <summary>
    /// Creates standard error data for activity execution failures
    /// </summary>
    public static Dictionary<string, object> CreateExecutionError(
        string activityId, 
        string message, 
        Exception? exception = null)
    {
        var errorData = new Dictionary<string, object>
        {
            ["error"] = "Activity execution failed",
            ["errorMessage"] = message,
            ["activityId"] = activityId,
            ["errorType"] = "ExecutionError",
            ["occurredAt"] = DateTime.UtcNow
        };

        if (exception != null)
        {
            errorData["exceptionType"] = exception.GetType().Name;
            errorData["stackTrace"] = exception.StackTrace ?? string.Empty;
        }

        return errorData;
    }

    /// <summary>
    /// Creates standard error data for activity resume failures
    /// </summary>
    public static Dictionary<string, object> CreateResumeError(
        string activityId, 
        string message, 
        string? reason = null)
    {
        var errorData = new Dictionary<string, object>
        {
            ["error"] = "Activity resume failed",
            ["errorMessage"] = message,
            ["activityId"] = activityId,
            ["errorType"] = "ResumeError"
        };

        if (!string.IsNullOrEmpty(reason))
        {
            errorData["reason"] = reason;
        }

        return errorData;
    }

    /// <summary>
    /// Creates standard error data for expression evaluation failures
    /// </summary>
    public static Dictionary<string, object> CreateExpressionError(
        string activityId, 
        string expression, 
        string message)
    {
        return new Dictionary<string, object>
        {
            ["error"] = "Expression evaluation failed",
            ["errorMessage"] = message,
            ["activityId"] = activityId,
            ["errorType"] = "ExpressionError",
            ["expression"] = expression,
            ["evaluatedAt"] = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates standard error data for missing required properties/variables
    /// </summary>
    public static Dictionary<string, object> CreateMissingPropertyError(
        string activityId, 
        string propertyName, 
        string propertyType = "property")
    {
        return new Dictionary<string, object>
        {
            ["error"] = $"Missing required {propertyType}",
            ["errorMessage"] = $"Missing required '{propertyName}' {propertyType}",
            ["activityId"] = activityId,
            ["errorType"] = "MissingPropertyError",
            ["missingProperty"] = propertyName,
            ["propertyType"] = propertyType
        };
    }

    /// <summary>
    /// Creates standard error data for unsupported operations
    /// </summary>
    public static Dictionary<string, object> CreateUnsupportedOperationError(
        string activityId, 
        string operation, 
        string? supportedOperations = null)
    {
        var errorData = new Dictionary<string, object>
        {
            ["error"] = "Unsupported operation",
            ["errorMessage"] = $"Operation '{operation}' is not supported",
            ["activityId"] = activityId,
            ["errorType"] = "UnsupportedOperationError",
            ["operation"] = operation
        };

        if (!string.IsNullOrEmpty(supportedOperations))
        {
            errorData["supportedOperations"] = supportedOperations;
        }

        return errorData;
    }
}