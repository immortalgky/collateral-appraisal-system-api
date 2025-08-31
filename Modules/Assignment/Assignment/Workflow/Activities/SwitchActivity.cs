using Assignment.Workflow.Activities.Core;
using Assignment.Workflow.Schema;
using Assignment.Workflow.Models;
using Assignment.Workflow.Engine.Expression;
using System.Text.RegularExpressions;

namespace Assignment.Workflow.Activities;

/// <summary>
/// SwitchActivity provides multi-branch conditional routing based on expression evaluation
/// Outputs case: matched_condition for transition-based routing
/// </summary>
public class SwitchActivity : WorkflowActivityBase
{
    private readonly ExpressionEvaluator _expressionEvaluator;
    private readonly ILogger<SwitchActivity> _logger;

    public SwitchActivity(ILogger<SwitchActivity> logger)
    {
        _expressionEvaluator = new ExpressionEvaluator();
        _logger = logger;
    }

    public override string ActivityType => ActivityTypes.SwitchActivity;
    public override string Name => "Switch Decision";
    public override string Description => "Multi-branch conditional routing with support for comparisons and value matching";

    protected override Task<ActivityResult> ExecuteActivityAsync(ActivityContext context, CancellationToken cancellationToken = default)
    {
        var expression = GetProperty<string>(context, "expression");
        
        try
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                _logger.LogError("SwitchActivity {ActivityId} missing required 'expression' property", context.ActivityId);
                return Task.FromResult(ActivityResult.Failed("Missing required 'expression' property", 
                    ActivityErrorBuilder.CreateMissingPropertyError(context.ActivityId, "expression")));
            }

            var cases = GetProperty<List<string>>(context, "cases");
            if (cases == null || !cases.Any())
            {
                _logger.LogError("SwitchActivity {ActivityId} missing or empty 'cases' property", context.ActivityId);
                return Task.FromResult(ActivityResult.Failed("Missing or empty 'cases' property", 
                    ActivityErrorBuilder.CreateMissingPropertyError(context.ActivityId, "cases")));
            }

            // Evaluate the base expression
            var expressionResult = _expressionEvaluator.EvaluateExpression<object>(expression, context.Variables);
            
            var outputData = new Dictionary<string, object>
            {
                ["expression"] = expression,
                ["expressionResult"] = expressionResult ?? "null",
                ["evaluatedAt"] = DateTime.Now
            };

            // Find matching case
            string? matchedCase = null;

            foreach (var caseCondition in cases)
            {
                if (EvaluateCaseCondition(caseCondition, expressionResult, context.Variables))
                {
                    matchedCase = caseCondition;
                    break;
                }
            }

            // If no case matched, use default
            if (matchedCase == null)
            {
                matchedCase = "default";
            }

            // Prepare output data
            outputData["case"] = matchedCase; // For transition evaluation

            _logger.LogInformation("SwitchActivity {ActivityId} evaluated expression '{Expression}' = '{Result}', matched case '{Case}'", 
                context.ActivityId, expression, expressionResult, matchedCase);

            // SwitchActivity completes immediately - no user interaction required
            return Task.FromResult(ActivityResult.Success(outputData));
        }
        catch (ExpressionEvaluationException ex)
        {
            _logger.LogError(ex, "SwitchActivity {ActivityId} failed to evaluate expression", context.ActivityId);
            return Task.FromResult(ActivityResult.Failed($"Expression evaluation failed: {ex.Message}", 
                ActivityErrorBuilder.CreateExpressionError(context.ActivityId, expression, ex.Message)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SwitchActivity {ActivityId} execution failed", context.ActivityId);
            return Task.FromResult(ActivityResult.Failed($"SwitchActivity execution failed: {ex.Message}", 
                ActivityErrorBuilder.CreateExecutionError(context.ActivityId, ex.Message, ex)));
        }
    }

    protected override Task<ActivityResult> ResumeActivityAsync(ActivityContext context, Dictionary<string, object> resumeInput, CancellationToken cancellationToken = default)
    {
        // SwitchActivity doesn't support resume - it completes immediately
        _logger.LogWarning("SwitchActivity {ActivityId} received unexpected resume call. SwitchActivity should complete immediately and not require resume.", 
            context.ActivityId);
        
        return Task.FromResult(ActivityResult.Failed("SwitchActivity does not support resume operations", 
            ActivityErrorBuilder.CreateUnsupportedOperationError(context.ActivityId, "resume", 
                "SwitchActivity completes immediately and does not support resume")));
    }

    public override Task<Core.ValidationResult> ValidateAsync(ActivityContext context, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        // Validate expression is provided
        var expression = GetProperty<string>(context, "expression");
        if (string.IsNullOrWhiteSpace(expression))
        {
            errors.Add("'expression' property is required for SwitchActivity");
        }
        else
        {
            // Validate expression syntax
            if (!_expressionEvaluator.ValidateExpression(expression, out var expressionError))
            {
                errors.Add($"Invalid expression syntax: {expressionError}");
            }
        }

        // Validate cases are provided
        var cases = GetProperty<List<string>>(context, "cases");
        if (cases == null || !cases.Any())
        {
            errors.Add("'cases' property is required and must contain at least one case for SwitchActivity");
        }
        else
        {
            // Validate case conditions
            foreach (var caseCondition in cases)
            {
                if (string.IsNullOrWhiteSpace(caseCondition))
                {
                    errors.Add("Case conditions cannot be empty");
                }

                // Validate case condition syntax if it's a comparison
                if (IsComparisonCondition(caseCondition))
                {
                    var testExpression = $"testValue {caseCondition}";
                    if (!_expressionEvaluator.ValidateExpression(testExpression, out var caseError))
                    {
                        errors.Add($"Invalid case condition '{caseCondition}': {caseError}");
                    }
                }
            }
        }

        return Task.FromResult(errors.Any() 
            ? Core.ValidationResult.Failure(errors.ToArray())
            : Core.ValidationResult.Success());
    }

    protected override WorkflowActivityExecution CreateActivityExecution(ActivityContext context)
    {
        // SwitchActivity executes immediately without assignment
        return WorkflowActivityExecution.Create(
            context.WorkflowInstance.Id,
            context.ActivityId,
            Name,
            ActivityType,
            "SYSTEM", // System-executed activity
            context.Variables);
    }

    /// <summary>
    /// Evaluates a case condition against the expression result
    /// Supports both comparison operators and direct value matching
    /// </summary>
    private bool EvaluateCaseCondition(string caseCondition, object? expressionResult, Dictionary<string, object> variables)
    {
        try
        {
            // Handle null/empty cases
            if (string.IsNullOrWhiteSpace(caseCondition))
                return false;

            // Check if this is a comparison condition (starts with operator)
            if (IsComparisonCondition(caseCondition))
            {
                // Build a comparison expression: expressionResult [operator] [value]
                var comparisonExpression = $"expressionValue {caseCondition}";
                
                // Create temporary variables for evaluation
                var tempVariables = new Dictionary<string, object>(variables)
                {
                    ["expressionValue"] = expressionResult ?? "null"
                };

                return _expressionEvaluator.EvaluateExpression(comparisonExpression, tempVariables);
            }
            else
            {
                // Direct value matching - convert both to strings for comparison
                var expressionStr = ConvertToComparableValue(expressionResult);
                var caseStr = ConvertToComparableValue(caseCondition);
                
                return string.Equals(expressionStr, caseStr, StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to evaluate case condition '{CaseCondition}' against value '{Value}'. Treating as non-match.", 
                caseCondition, expressionResult);
            return false;
        }
    }

    /// <summary>
    /// Determines if a case condition is a comparison operator (e.g., "< 100", ">= 50000")
    /// </summary>
    private bool IsComparisonCondition(string condition)
    {
        // Match comparison operators at the start of the condition
        var comparisonPattern = @"^\s*(>=|<=|!=|==|>|<)\s*";
        return Regex.IsMatch(condition, comparisonPattern);
    }

    /// <summary>
    /// Converts a value to a string representation suitable for comparison
    /// </summary>
    private string ConvertToComparableValue(object? value)
    {
        if (value == null) return "null";
        if (value is string str) return str;
        if (value is bool b) return b.ToString().ToLowerInvariant();
        return value.ToString() ?? "null";
    }
}