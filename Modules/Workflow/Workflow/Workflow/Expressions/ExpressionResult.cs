namespace Workflow.Workflow.Expressions;

/// <summary>
/// Result of an expression evaluation
/// </summary>
/// <typeparam name="T">Type of the expression result</typeparam>
public class ExpressionResult<T>
{
    /// <summary>
    /// The evaluated result value
    /// </summary>
    public T? Value { get; set; }

    /// <summary>
    /// Whether evaluation was successful
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Error message if evaluation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Exception that occurred during evaluation
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Time taken for evaluation
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }

    /// <summary>
    /// Variables that were accessed during evaluation
    /// </summary>
    public List<string> AccessedVariables { get; set; } = new();

    /// <summary>
    /// Functions that were called during evaluation
    /// </summary>
    public List<string> CalledFunctions { get; set; } = new();

    /// <summary>
    /// Create a successful result
    /// </summary>
    public static ExpressionResult<T> Success(T value, TimeSpan executionTime = default)
    {
        return new ExpressionResult<T>
        {
            Value = value,
            IsSuccess = true,
            ExecutionTime = executionTime
        };
    }

    /// <summary>
    /// Create a failed result
    /// </summary>
    public static ExpressionResult<T> Failure(string errorMessage, Exception? exception = null)
    {
        return new ExpressionResult<T>
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            Exception = exception
        };
    }
}