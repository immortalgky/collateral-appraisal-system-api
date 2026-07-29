using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Workflow.Data;
using Workflow.Data.Entities;
using Workflow.Workflow.Engine.Expression;

namespace Workflow.Services.Configuration;

/// <summary>
/// Matches workflow variables against the active <see cref="AutoAssignmentRule"/> rows.
///
/// Matching is deliberately conservative: a rule only wins when EVERY populated condition matches,
/// and a rule whose <see cref="AutoAssignmentRule.ConditionExpression"/> fails to evaluate is
/// skipped rather than treated as a match. A misconfigured rule therefore falls through to the
/// next rule — and ultimately to the workflow definition's own routing conditions — instead of
/// silently redirecting cases.
/// </summary>
public class AutoAssignmentRuleService(
    WorkflowDbContext db,
    IExpressionEvaluator expressionEvaluator,
    ILogger<AutoAssignmentRuleService> logger) : IAutoAssignmentRuleService
{
    public async Task<AutoAssignmentRule?> FindMatchingRuleAsync(
        IReadOnlyDictionary<string, object> variables,
        CancellationToken ct = default)
    {
        var rules = await db.AutoAssignmentRules
            .AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.RuleName)
            .ToListAsync(ct);

        if (rules.Count == 0) return null;

        foreach (var rule in rules)
        {
            if (Matches(rule, variables))
            {
                logger.LogInformation(
                    "AutoAssignmentRule '{RuleName}' (priority {Priority}) matched → {Decision}",
                    rule.RuleName, rule.Priority, rule.RoutingDecision);
                return rule;
            }
        }

        logger.LogInformation(
            "No AutoAssignmentRule matched across {Count} active rule(s); falling back to the workflow definition",
            rules.Count);
        return null;
    }

    private bool Matches(AutoAssignmentRule rule, IReadOnlyDictionary<string, object> variables)
    {
        if (!MatchesCsv(rule.Channels, GetString(variables, "channel"))) return false;
        if (!MatchesCsv(rule.EntrySources, GetString(variables, "entrySource"))) return false;
        if (!MatchesCsv(rule.LoanTypes, GetString(variables, "bankingSegment"))) return false;
        if (!MatchesCsv(rule.Priorities, GetString(variables, "priority"))) return false;

        if (rule.MinFacilityLimit.HasValue || rule.MaxFacilityLimit.HasValue)
        {
            var facilityLimit = GetDecimal(variables, "facilityLimit");
            if (rule.MinFacilityLimit.HasValue && facilityLimit < rule.MinFacilityLimit.Value) return false;
            if (rule.MaxFacilityLimit.HasValue && facilityLimit > rule.MaxFacilityLimit.Value) return false;
        }

        if (!string.IsNullOrWhiteSpace(rule.ConditionExpression))
        {
            try
            {
                // Same evaluator and same bare-variable syntax as the routingConditions in
                // appraisal-workflow.json, so a rule expression reads identically to the JSON
                // condition it replaces (e.g. "isPma == true").
                if (!expressionEvaluator.EvaluateExpression(
                        rule.ConditionExpression,
                        new Dictionary<string, object>(variables)))
                    return false;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "AutoAssignmentRule '{RuleName}' has an unevaluable ConditionExpression '{Expression}'; skipping the rule",
                    rule.RuleName, rule.ConditionExpression);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// A null/blank CSV condition matches anything. Otherwise the variable must equal one of the
    /// listed values (case-insensitive). An empty variable never matches a populated condition.
    /// </summary>
    private static bool MatchesCsv(string? csv, string? value)
    {
        if (string.IsNullOrWhiteSpace(csv)) return true;
        if (string.IsNullOrWhiteSpace(value)) return false;

        return csv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(entry => string.Equals(entry, value, StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetString(IReadOnlyDictionary<string, object> variables, string key) =>
        variables.TryGetValue(key, out var value) ? value?.ToString() : null;

    private static decimal GetDecimal(IReadOnlyDictionary<string, object> variables, string key)
    {
        if (!variables.TryGetValue(key, out var value) || value is null) return 0m;

        return value switch
        {
            decimal d => d,
            double dbl => (decimal)dbl,
            int i => i,
            long l => l,
            _ => decimal.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0m
        };
    }
}
