using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Workflow.Data;
using Workflow.Workflow.Engine.Expression;

namespace Workflow.Workflow.Pipeline;

/// <summary>
/// Evaluates whether appraisal creation should be triggered based on
/// ActivityProcessConfiguration rows. Used by both the consumer (immediate path)
/// and the pipeline step (deferred path).
/// </summary>
public class AppraisalCreationTriggerEvaluator(
    WorkflowDbContext dbContext,
    IExpressionEvaluator expressionEvaluator,
    ILogger<AppraisalCreationTriggerEvaluator> logger)
{
    /// <summary>
    /// Reads config rows for the given activity name and evaluates whether
    /// the appraisal creation condition is satisfied.
    /// </summary>
    public async Task<bool> ShouldEmitAsync(
        string activityName,
        Dictionary<string, object> variables,
        Dictionary<string, object>? input = null,
        CancellationToken ct = default)
    {
        var configs = await dbContext.ActivityProcessConfigurations
            .Where(c => c.ActivityName == activityName
                        && c.ProcessorName == "EmitAppraisalCreationRequested"
                        && c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct);

        if (configs.Count == 0)
        {
            logger.LogDebug("No EmitAppraisalCreationRequested config for activity {ActivityName}", activityName);
            return false;
        }

        foreach (var config in configs)
        {
            if (EvaluateConfig(config.Parameters, variables, input))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Evaluates a single config row's condition and optional requireDecision check.
    /// </summary>
    public bool EvaluateConfig(
        string? parameters,
        Dictionary<string, object> variables,
        Dictionary<string, object>? input = null)
    {
        if (string.IsNullOrWhiteSpace(parameters))
            return true;

        try
        {
            using var doc = JsonDocument.Parse(parameters);
            var root = doc.RootElement;

            // Check requireDecision against input
            if (root.TryGetProperty("requireDecision", out var reqDecision))
            {
                var requiredValue = reqDecision.GetString();
                if (!string.IsNullOrEmpty(requiredValue))
                {
                    var decisionField = root.TryGetProperty("decisionField", out var df)
                        ? df.GetString() ?? "decisionTaken"
                        : "decisionTaken";

                    if (input is null || !input.TryGetValue(decisionField, out var rawDecision))
                        return false;

                    var actual = rawDecision switch
                    {
                        JsonElement je => je.GetString() ?? je.ToString(),
                        string s => s,
                        _ => rawDecision?.ToString()
                    };

                    if (!string.Equals(actual, requiredValue, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }

            // Evaluate condition expression
            if (root.TryGetProperty("condition", out var conditionElement))
            {
                var condition = conditionElement.GetString();
                if (!string.IsNullOrWhiteSpace(condition))
                {
                    var result = expressionEvaluator.EvaluateExpression(condition, variables);
                    logger.LogDebug(
                        "Condition '{Condition}' evaluated to {Result}", condition, result);
                    return result;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to evaluate trigger config: {Parameters}", parameters);
            return false;
        }
    }
}
