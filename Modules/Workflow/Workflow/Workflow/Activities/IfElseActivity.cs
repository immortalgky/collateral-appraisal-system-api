using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Schema;
using Workflow.Workflow.Models;
using Workflow.Workflow.Engine.Expression;

namespace Workflow.Workflow.Activities;

/// <summary>
/// IfElseActivity provides binary conditional routing based on a single boolean expression
/// Outputs result: true/false for transition-based routing
/// </summary>
public class IfElseActivity : WorkflowActivityBase
{
    private readonly ExpressionEvaluator _expressionEvaluator;
    private readonly ILogger<IfElseActivity> _logger;

    public IfElseActivity(ILogger<IfElseActivity> logger)
    {
        _expressionEvaluator = new ExpressionEvaluator();
        _logger = logger;
    }

    public override string ActivityType => ActivityTypes.IfElseActivity;
    public override string Name => "If-Else Decision";
    public override string Description => "Binary conditional routing based on boolean expression evaluation";

    protected override Task<ActivityResult> ExecuteActivityAsync(ActivityContext context, CancellationToken cancellationToken = default)
    {
        var condition = GetProperty<string>(context, "condition");
        
        try
        {
            if (string.IsNullOrWhiteSpace(condition))
            {
                _logger.LogError("IfElseActivity {ActivityId} missing required 'condition' property", context.ActivityId);
                return Task.FromResult(ActivityResult.Failed("Missing required 'condition' property", 
                    ActivityErrorBuilder.CreateMissingPropertyError(context.ActivityId, "condition")));
            }

            // Evaluate the boolean condition
            var conditionResult = _expressionEvaluator.EvaluateExpression(condition, context.Variables);
            
            var outputData = new Dictionary<string, object>
            {
                ["condition"] = condition,
                ["result"] = conditionResult, // Boolean output for transition evaluation
                ["evaluatedAt"] = DateTime.Now
            };

            _logger.LogInformation("IfElseActivity {ActivityId} evaluated condition '{Condition}' = {Result}", 
                context.ActivityId, condition, conditionResult);

            // IfElseActivity completes immediately - no user interaction required
            return Task.FromResult(ActivityResult.Success(outputData));
        }
        catch (ExpressionEvaluationException ex)
        {
            _logger.LogError(ex, "IfElseActivity {ActivityId} failed to evaluate condition", context.ActivityId);
            return Task.FromResult(ActivityResult.Failed($"Condition evaluation failed: {ex.Message}", 
                ActivityErrorBuilder.CreateExpressionError(context.ActivityId, condition, ex.Message)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IfElseActivity {ActivityId} execution failed", context.ActivityId);
            return Task.FromResult(ActivityResult.Failed($"IfElseActivity execution failed: {ex.Message}", 
                ActivityErrorBuilder.CreateExecutionError(context.ActivityId, ex.Message, ex)));
        }
    }

    protected override Task<ActivityResult> ResumeActivityAsync(ActivityContext context, Dictionary<string, object> resumeInput, CancellationToken cancellationToken = default)
    {
        // IfElseActivity doesn't support resume - it completes immediately
        _logger.LogWarning("IfElseActivity {ActivityId} received unexpected resume call. IfElseActivity should complete immediately and not require resume.", 
            context.ActivityId);
        
        return Task.FromResult(ActivityResult.Failed("IfElseActivity does not support resume operations", 
            ActivityErrorBuilder.CreateUnsupportedOperationError(context.ActivityId, "resume", 
                "IfElseActivity completes immediately and does not support resume")));
    }

    public override Task<Core.ValidationResult> ValidateAsync(ActivityContext context, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        // Validate condition is provided
        var condition = GetProperty<string>(context, "condition");
        if (string.IsNullOrWhiteSpace(condition))
        {
            errors.Add("'condition' property is required for IfElseActivity");
        }
        else
        {
            // Validate condition syntax
            if (!_expressionEvaluator.ValidateExpression(condition, out var conditionError))
            {
                errors.Add($"Invalid condition syntax: {conditionError}");
            }
        }

        // No additional validation needed - routing handled by transitions

        return Task.FromResult(errors.Any() 
            ? Core.ValidationResult.Failure(errors.ToArray())
            : Core.ValidationResult.Success());
    }

    protected override WorkflowActivityExecution CreateActivityExecution(ActivityContext context)
    {
        // IfElseActivity executes immediately without assignment
        return WorkflowActivityExecution.Create(
            context.WorkflowInstance.Id,
            context.ActivityId,
            Name,
            ActivityType,
            "SYSTEM", // System-executed activity
            context.Variables);
    }
}