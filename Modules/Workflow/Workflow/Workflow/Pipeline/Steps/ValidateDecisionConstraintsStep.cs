using System.Text.Json;
using Microsoft.Extensions.Logging;
using Workflow.Data.Entities;
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
    public sealed record Parameters
    {
        /// <summary>Name of the input field carrying the decision value (e.g., "decision").</summary>
        public string DecisionField { get; init; } = "decision";

        /// <summary>
        /// Map of decision value → constraint expression.
        /// e.g. {"INT": "workflow.variables.facilityLimit &lt;= 50000000"}
        /// </summary>
        public Dictionary<string, string>? Constraints { get; init; }
    }

    public StepDescriptor Descriptor { get; } = StepDescriptor.For<Parameters>(
        name: "ValidateDecisionConstraints",
        displayName: "Validate Decision Constraints",
        kind: StepKind.Validation,
        description: "Blocks a decision value when its associated constraint expression is not satisfied.");

    public Task<ProcessStepResult> ExecuteAsync(ProcessStepContext ctx, CancellationToken ct)
    {
        var p = ctx.GetParameters<Parameters>();

        if (p.Constraints is null || p.Constraints.Count == 0)
            return Task.FromResult(ProcessStepResult.Pass());

        var decisionField = p.DecisionField;

        if (!ctx.Input.TryGetValue(decisionField, out var rawDecision))
            return Task.FromResult(ProcessStepResult.Pass());

        var decision = rawDecision switch
        {
            JsonElement je => je.GetString() ?? je.ToString(),
            string s => s,
            _ => rawDecision?.ToString()
        };

        if (string.IsNullOrEmpty(decision))
            return Task.FromResult(ProcessStepResult.Pass());

        if (!p.Constraints.TryGetValue(decision, out var expression))
            return Task.FromResult(ProcessStepResult.Pass());

        if (string.IsNullOrWhiteSpace(expression))
            return Task.FromResult(ProcessStepResult.Pass());

        // Convert the read-only variables dict back to mutable for the legacy evaluator
        var variables = ctx.Variables.ToDictionary(kv => kv.Key, kv => kv.Value ?? (object)"");

        try
        {
            var satisfied = expressionEvaluator.EvaluateExpression(expression, variables);

            if (!satisfied)
            {
                logger.LogWarning(
                    "Decision constraint failed for activity {ActivityName}: decision '{Decision}' blocked by '{Expression}'",
                    ctx.ActivityName, decision, expression);

                return Task.FromResult(ProcessStepResult.Fail(
                    "DECISION_CONSTRAINT_FAILED",
                    $"Decision '{decision}' is not allowed: constraint '{expression}' not met"));
            }

            return Task.FromResult(ProcessStepResult.Pass());
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to evaluate decision constraint '{Expression}' for activity {ActivityName}",
                expression, ctx.ActivityName);

            return Task.FromResult(ProcessStepResult.Error(ex));
        }
    }
}
