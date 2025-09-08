namespace Workflow.Workflow.Engine.Expression;

/// <summary>
/// Service for evaluating expressions within workflow contexts
/// Supports variable substitution and condition evaluation
/// </summary>
public interface IWorkflowExpressionEvaluator
{
    /// <summary>
    /// Evaluates a boolean expression using the provided variables
    /// </summary>
    /// <param name="expression">Expression to evaluate</param>
    /// <param name="variables">Available variables for the expression</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Boolean result of the expression</returns>
    Task<bool> EvaluateBooleanAsync(
        string expression,
        Dictionary<string, object> variables,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Evaluates an expression and returns the result as an object
    /// </summary>
    /// <param name="expression">Expression to evaluate</param>
    /// <param name="variables">Available variables for the expression</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the expression evaluation</returns>
    Task<object?> EvaluateAsync(
        string expression,
        Dictionary<string, object> variables,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Substitutes variable placeholders in a string with their values
    /// </summary>
    /// <param name="template">Template string with variable placeholders (e.g., "Hello ${name}")</param>
    /// <param name="variables">Available variables for substitution</param>
    /// <returns>String with variables substituted</returns>
    string SubstituteVariables(string template, Dictionary<string, object> variables);
    
    /// <summary>
    /// Validates that an expression is syntactically correct
    /// </summary>
    /// <param name="expression">Expression to validate</param>
    /// <returns>Validation result</returns>
    Task<ExpressionValidationResult> ValidateExpressionAsync(string expression);
}

/// <summary>
/// Result of expression validation
/// </summary>
public class ExpressionValidationResult
{
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }
    public List<string> Warnings { get; init; } = new();
    
    public static ExpressionValidationResult Valid(List<string>? warnings = null) 
        => new() { IsValid = true, Warnings = warnings ?? new List<string>() };
    
    public static ExpressionValidationResult Invalid(string errorMessage, List<string>? warnings = null)
        => new() { IsValid = false, ErrorMessage = errorMessage, Warnings = warnings ?? new List<string>() };
}