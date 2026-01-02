namespace Workflow.Workflow.Expressions;

/// <summary>
/// Metadata about an expression without executing it
/// </summary>
public class ExpressionMetadata
{
    /// <summary>
    /// Original expression text
    /// </summary>
    public string ExpressionText { get; set; } = string.Empty;

    /// <summary>
    /// Variables referenced in the expression
    /// </summary>
    public List<string> ReferencedVariables { get; set; } = new();

    /// <summary>
    /// Functions called in the expression
    /// </summary>
    public List<string> ReferencedFunctions { get; set; } = new();

    /// <summary>
    /// Return type of the expression
    /// </summary>
    public Type? ReturnType { get; set; }

    /// <summary>
    /// Whether the expression is syntactically valid
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Syntax errors if any
    /// </summary>
    public List<string> SyntaxErrors { get; set; } = new();

    /// <summary>
    /// Complexity score for optimization decisions
    /// </summary>
    public int ComplexityScore { get; set; }

    /// <summary>
    /// Whether the expression has side effects
    /// </summary>
    public bool HasSideEffects { get; set; }

    /// <summary>
    /// Whether the expression is deterministic (same input = same output)
    /// </summary>
    public bool IsDeterministic { get; set; }

    /// <summary>
    /// Estimated execution time category
    /// </summary>
    public ExpressionPerformanceCategory PerformanceCategory { get; set; }

    /// <summary>
    /// Security risk level of the expression
    /// </summary>
    public ExpressionSecurityLevel SecurityLevel { get; set; }
}

/// <summary>
/// Performance categories for expressions
/// </summary>
public enum ExpressionPerformanceCategory
{
    Fast,      // < 1ms
    Medium,    // 1-10ms  
    Slow,      // 10-100ms
    VerySlow   // > 100ms
}

/// <summary>
/// Security levels for expressions
/// </summary>
public enum ExpressionSecurityLevel
{
    Safe,       // No security concerns
    Low,        // Minor concerns (e.g., resource usage)
    Medium,     // Moderate concerns (e.g., file access)
    High,       // High concerns (e.g., network access)
    Restricted  // Should be blocked
}