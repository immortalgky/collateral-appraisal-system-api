namespace Assignment.Workflow.Engine.Expression;

/// <summary>
/// Interface for expression evaluation service
/// Enables dependency injection and reduces duplicate ExpressionEvaluator instances
/// </summary>
public interface IExpressionEvaluator
{
    /// <summary>
    /// Evaluates a boolean expression using the provided variables
    /// </summary>
    bool EvaluateExpression(string expression, Dictionary<string, object> variables);

    /// <summary>
    /// Evaluates an expression and returns the result as the specified type
    /// </summary>
    T EvaluateExpression<T>(string expression, Dictionary<string, object> variables);

    /// <summary>
    /// Validates an expression syntax without executing it
    /// </summary>
    bool ValidateExpression(string expression, out string? errorMessage);
}