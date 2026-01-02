namespace Workflow.Workflow.ImportExport;

/// <summary>
/// Result of workflow validation operation
/// </summary>
public class WorkflowValidationResult
{
    /// <summary>
    /// Whether validation passed
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Validation errors
    /// </summary>
    public List<WorkflowValidationError> Errors { get; set; } = new();

    /// <summary>
    /// Validation warnings
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Validation execution time
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }

    /// <summary>
    /// Number of workflows validated
    /// </summary>
    public int ValidatedWorkflowCount { get; set; }

    /// <summary>
    /// Validation metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Validation statistics
    /// </summary>
    public WorkflowValidationStatistics Statistics { get; set; } = new();

    /// <summary>
    /// Create a successful validation result
    /// </summary>
    public static WorkflowValidationResult Success(TimeSpan executionTime, int workflowCount = 1)
    {
        return new WorkflowValidationResult
        {
            IsValid = true,
            ExecutionTime = executionTime,
            ValidatedWorkflowCount = workflowCount
        };
    }

    /// <summary>
    /// Create a failed validation result
    /// </summary>
    public static WorkflowValidationResult Failure(List<WorkflowValidationError> errors)
    {
        return new WorkflowValidationResult
        {
            IsValid = false,
            Errors = errors
        };
    }
}

/// <summary>
/// Validation error details
/// </summary>
public class WorkflowValidationError
{
    public string Message { get; set; } = string.Empty;
    public string? WorkflowName { get; set; }
    public string? ActivityId { get; set; }
    public string? PropertyPath { get; set; }
    public ValidationErrorType ErrorType { get; set; }
    public ValidationSeverity Severity { get; set; }
    public int? LineNumber { get; set; }
    public int? ColumnNumber { get; set; }
    public string ErrorCode { get; set; } = string.Empty;
}

/// <summary>
/// Validation statistics
/// </summary>
public class WorkflowValidationStatistics
{
    public int TotalActivities { get; set; }
    public int ValidActivities { get; set; }
    public int InvalidActivities { get; set; }
    public int TotalConnections { get; set; }
    public int ValidConnections { get; set; }
    public int InvalidConnections { get; set; }
    public int TotalExpressions { get; set; }
    public int ValidExpressions { get; set; }
    public int InvalidExpressions { get; set; }
    public Dictionary<ValidationErrorType, int> ErrorTypeCounts { get; set; } = new();
}

/// <summary>
/// Types of validation errors
/// </summary>
public enum ValidationErrorType
{
    SyntaxError,
    SchemaViolation,
    MissingProperty,
    InvalidProperty,
    CircularReference,
    UnresolvedReference,
    InvalidExpression,
    InvalidConnection,
    MissingActivity,
    DuplicateId,
    SecurityViolation
}

/// <summary>
/// Severity levels for validation errors
/// </summary>
public enum ValidationSeverity
{
    Info,
    Warning,
    Error,
    Critical
}