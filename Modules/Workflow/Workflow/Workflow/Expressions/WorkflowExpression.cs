namespace Workflow.Workflow.Expressions;

/// <summary>
/// Compiled workflow expression for efficient repeated evaluation
/// </summary>
/// <typeparam name="T">Return type of the expression</typeparam>
public class WorkflowExpression<T>
{
    /// <summary>
    /// Original expression text
    /// </summary>
    public string ExpressionText { get; set; } = string.Empty;

    /// <summary>
    /// Compiled delegate for execution
    /// </summary>
    public Func<ExpressionContext, T>? CompiledDelegate { get; set; }

    /// <summary>
    /// Variables required by this expression
    /// </summary>
    public List<string> RequiredVariables { get; set; } = new();

    /// <summary>
    /// Functions used by this expression
    /// </summary>
    public List<string> UsedFunctions { get; set; } = new();

    /// <summary>
    /// Whether the expression is valid and compiled
    /// </summary>
    public bool IsCompiled { get; set; }

    /// <summary>
    /// Compilation error if any
    /// </summary>
    public string? CompilationError { get; set; }

    /// <summary>
    /// When the expression was compiled
    /// </summary>
    public DateTime CompiledAt { get; set; }

    /// <summary>
    /// Expression complexity score (for caching and optimization decisions)
    /// </summary>
    public int ComplexityScore { get; set; }

    /// <summary>
    /// Evaluate the compiled expression
    /// </summary>
    public T? Evaluate(ExpressionContext context)
    {
        if (!IsCompiled || CompiledDelegate == null)
        {
            throw new InvalidOperationException("Expression is not compiled");
        }

        return CompiledDelegate(context);
    }
}