namespace Workflow.Workflow.Expressions;

/// <summary>
/// Service for evaluating and managing workflow expressions using C# scripting
/// </summary>
public interface IWorkflowExpressionService
{
    /// <summary>
    /// Evaluate a C# expression within a workflow context
    /// </summary>
    Task<ExpressionResult<T>> EvaluateAsync<T>(string expression, ExpressionContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluate a boolean expression for conditional logic
    /// </summary>
    Task<bool> EvaluateBooleanAsync(string expression, ExpressionContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate an expression syntax without executing it
    /// </summary>
    Task<bool> ValidateExpressionAsync(string expression, CancellationToken cancellationToken = default);

    /// <summary>
    /// Parse expression and return metadata about variables used
    /// </summary>
    Task<ExpressionMetadata> ParseExpressionAsync(string expression, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a compiled expression for repeated evaluation
    /// </summary>
    Task<WorkflowExpression<T>> CompileAsync<T>(string expression, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluate a pre-compiled expression
    /// </summary>
    Task<ExpressionResult<T>> EvaluateCompiledAsync<T>(WorkflowExpression<T> compiledExpression, ExpressionContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get available functions and variables in the current context
    /// </summary>
    Task<IEnumerable<string>> GetAvailableFunctionsAsync(CancellationToken cancellationToken = default);
}