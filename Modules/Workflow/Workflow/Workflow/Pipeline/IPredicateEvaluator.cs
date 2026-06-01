namespace Workflow.Workflow.Pipeline;

/// <summary>
/// Evaluates a sandboxed JavaScript predicate expression against a step context.
/// Compile cache is keyed by (ConfigurationId, Version) for fast repeated evaluation.
/// </summary>
public interface IPredicateEvaluator
{
    /// <summary>
    /// Evaluates the expression. Returns true to run the step, false to skip.
    /// Throws <see cref="PredicateEvaluationException"/> if the expression is malformed,
    /// times out, or returns a non-boolean value.
    /// </summary>
    bool Evaluate(string expression, Guid configurationId, int configurationVersion, ProcessStepContext ctx);

    /// <summary>
    /// Evaluates an arbitrary boolean expression (e.g. a per-rule escape-hatch expression)
    /// against the provided context. The compiled script is cached by a stable hash of
    /// <paramref name="expression"/> — NOT by (configId, version) — to avoid collisions
    /// when multiple rules on the same config row have different expressions.
    /// Throws <see cref="PredicateEvaluationException"/> on compile/runtime error or
    /// when the expression returns a non-boolean.
    /// </summary>
    bool EvaluateExpression(string expression, ProcessStepContext ctx);

    /// <summary>
    /// Attempts to compile the expression without executing it (for admin validation).
    /// Returns null on success; an error message on failure.
    /// </summary>
    string? TryPrepare(string expression);
}

/// <summary>
/// Thrown when a RunIfExpression cannot be evaluated safely.
/// </summary>
public sealed class PredicateEvaluationException(string message, Exception? inner = null)
    : Exception(message, inner);
