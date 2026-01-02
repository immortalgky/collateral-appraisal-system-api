using System.Text.RegularExpressions;

namespace Workflow.Workflow.Engine.Expression;

/// <summary>
/// Simple implementation of workflow expression evaluator
/// Supports basic boolean expressions and variable substitution
/// Can be enhanced with more sophisticated expression engines like NCalc or similar
/// </summary>
public class WorkflowExpressionEvaluator : IWorkflowExpressionEvaluator
{
    private readonly ILogger<WorkflowExpressionEvaluator> _logger;
    private readonly Regex _variablePattern = new(@"\$\{([^}]+)\}", RegexOptions.Compiled);

    public WorkflowExpressionEvaluator(ILogger<WorkflowExpressionEvaluator> logger)
    {
        _logger = logger;
    }

    public Task<bool> EvaluateBooleanAsync(
        string expression,
        Dictionary<string, object> variables,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // First substitute variables
            var substitutedExpression = SubstituteVariables(expression, variables);
            
            _logger.LogDebug("Evaluating boolean expression: '{Original}' -> '{Substituted}'",
                expression, substitutedExpression);

            // Handle simple boolean literals
            if (bool.TryParse(substitutedExpression.Trim(), out var boolResult))
            {
                return Task.FromResult(boolResult);
            }

            // Handle simple equality checks
            if (substitutedExpression.Contains("=="))
            {
                var parts = substitutedExpression.Split("==", 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    var left = parts[0].Trim().Trim('"');
                    var right = parts[1].Trim().Trim('"');
                    return Task.FromResult(string.Equals(left, right, StringComparison.OrdinalIgnoreCase));
                }
            }

            // Handle simple inequality checks
            if (substitutedExpression.Contains("!="))
            {
                var parts = substitutedExpression.Split("!=", 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    var left = parts[0].Trim().Trim('"');
                    var right = parts[1].Trim().Trim('"');
                    return Task.FromResult(!string.Equals(left, right, StringComparison.OrdinalIgnoreCase));
                }
            }

            // Handle null/empty checks
            var trimmed = substitutedExpression.Trim();
            if (trimmed.StartsWith("!"))
            {
                var value = trimmed[1..].Trim();
                return Task.FromResult(string.IsNullOrWhiteSpace(value) || value.Equals("null", StringComparison.OrdinalIgnoreCase));
            }

            // Default to treating non-empty, non-null values as true
            var result = !string.IsNullOrWhiteSpace(substitutedExpression) && 
                        !substitutedExpression.Equals("null", StringComparison.OrdinalIgnoreCase) &&
                        !substitutedExpression.Equals("false", StringComparison.OrdinalIgnoreCase);

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to evaluate boolean expression '{Expression}'. Returning false.", expression);
            return Task.FromResult(false);
        }
    }

    public Task<object?> EvaluateAsync(
        string expression,
        Dictionary<string, object> variables,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var substitutedExpression = SubstituteVariables(expression, variables);
            
            _logger.LogDebug("Evaluating expression: '{Original}' -> '{Substituted}'",
                expression, substitutedExpression);

            // For now, just return the substituted string
            // This could be enhanced with a proper expression engine for mathematical operations, etc.
            return Task.FromResult<object?>(substitutedExpression);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to evaluate expression '{Expression}'. Returning null.", expression);
            return Task.FromResult<object?>(null);
        }
    }

    public string SubstituteVariables(string template, Dictionary<string, object> variables)
    {
        if (string.IsNullOrEmpty(template))
            return template;

        return _variablePattern.Replace(template, match =>
        {
            var variableName = match.Groups[1].Value;
            
            if (variables.TryGetValue(variableName, out var value))
            {
                return value?.ToString() ?? "";
            }

            _logger.LogDebug("Variable '{VariableName}' not found during substitution", variableName);
            return match.Value; // Keep original placeholder if variable not found
        });
    }

    public Task<ExpressionValidationResult> ValidateExpressionAsync(string expression)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return Task.FromResult(ExpressionValidationResult.Invalid("Expression cannot be empty"));
            }

            var warnings = new List<string>();

            // Check for balanced braces in variable placeholders
            var openBraces = expression.Count(c => c == '{');
            var closeBraces = expression.Count(c => c == '}');
            
            if (openBraces != closeBraces)
            {
                return Task.FromResult(ExpressionValidationResult.Invalid("Unbalanced braces in expression"));
            }

            // Check for valid variable placeholder format
            var matches = _variablePattern.Matches(expression);
            foreach (Match match in matches)
            {
                var variableName = match.Groups[1].Value;
                if (string.IsNullOrWhiteSpace(variableName))
                {
                    return Task.FromResult(ExpressionValidationResult.Invalid("Empty variable name in placeholder"));
                }

                // Check for valid variable naming (basic validation)
                if (!IsValidVariableName(variableName))
                {
                    warnings.Add($"Variable name '{variableName}' may not follow naming conventions");
                }
            }

            // Check for potential expression operators
            if (expression.Contains("===") || expression.Contains("!=="))
            {
                warnings.Add("Triple equality operators are not supported, use == or != instead");
            }

            return Task.FromResult(ExpressionValidationResult.Valid(warnings));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error validating expression '{Expression}'", expression);
            return Task.FromResult(ExpressionValidationResult.Invalid($"Validation error: {ex.Message}"));
        }
    }

    private static bool IsValidVariableName(string variableName)
    {
        // Basic validation - variable names should contain only letters, numbers, dots, and underscores
        return Regex.IsMatch(variableName, @"^[a-zA-Z_][a-zA-Z0-9_.]*$");
    }
}