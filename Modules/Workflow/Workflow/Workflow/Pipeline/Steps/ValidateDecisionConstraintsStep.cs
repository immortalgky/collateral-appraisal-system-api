using System.Text.Json;
using Microsoft.Extensions.Logging;
using Workflow.Workflow.Engine.Expression;

namespace Workflow.Workflow.Pipeline.Steps;

/// <summary>
/// Validates that the decision taken satisfies any constraint expression
/// defined in the step configuration. For example, "INT" may only be chosen
/// when facilityLimit &lt;= 50 000 000.
/// </summary>
public class ValidateDecisionConstraintsStep(
    IExpressionEvaluator expressionEvaluator,
    ILogger<ValidateDecisionConstraintsStep> logger) : IActivityProcessStep
{
    public string Name => "ValidateDecisionConstraints";

    public Task<ProcessStepResult> ExecuteAsync(ProcessStepContext context, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(context.Parameters))
            return Task.FromResult(ProcessStepResult.Ok());

        var parameters = JsonSerializer.Deserialize<JsonElement>(context.Parameters);

        if (!parameters.TryGetProperty("decisionField", out var decisionFieldElement))
            return Task.FromResult(ProcessStepResult.Ok());

        var decisionField = decisionFieldElement.GetString()!;

        // Get the decision value from input
        if (!context.Input.TryGetValue(decisionField, out var rawDecision))
            return Task.FromResult(ProcessStepResult.Ok());

        var decision = rawDecision switch
        {
            JsonElement je => je.GetString() ?? je.ToString(),
            string s => s,
            _ => rawDecision?.ToString()
        };

        if (string.IsNullOrEmpty(decision))
            return Task.FromResult(ProcessStepResult.Ok());

        // Look up constraint for this decision value
        if (!parameters.TryGetProperty("constraints", out var constraints)
            || !constraints.TryGetProperty(decision, out var constraintElement))
        {
            // No constraint defined for this decision — allow it
            return Task.FromResult(ProcessStepResult.Ok());
        }

        var expression = constraintElement.GetString();
        if (string.IsNullOrWhiteSpace(expression))
            return Task.FromResult(ProcessStepResult.Ok());

        // Evaluate the constraint expression against workflow variables
        var variables = context.Variables ?? new Dictionary<string, object>();

        try
        {
            var satisfied = expressionEvaluator.EvaluateExpression(expression, variables);

            if (!satisfied)
            {
                logger.LogWarning(
                    "Decision constraint failed for activity {ActivityName}: decision '{Decision}' blocked by '{Expression}'",
                    context.ActivityName, decision, expression);

                return Task.FromResult(
                    ProcessStepResult.Fail($"Decision '{decision}' is not allowed: constraint '{expression}' not met"));
            }

            return Task.FromResult(ProcessStepResult.Ok());
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to evaluate decision constraint '{Expression}' for activity {ActivityName}",
                expression, context.ActivityName);

            return Task.FromResult(
                ProcessStepResult.Fail($"Failed to evaluate constraint: {ex.Message}"));
        }
    }
}
