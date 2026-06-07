using System.ComponentModel;
using System.Text;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Extensions.Logging;
using Shared.Data;
using Workflow.Data.Entities;
using Workflow.Workflow.Pipeline.Validation;

namespace Workflow.Workflow.Pipeline.Steps;

/// <summary>
/// Generic config-driven validation step. Evaluates one or more field rules
/// (or Jint expression escape-hatch rules) against the appraisal's persisted data
/// read from <c>appraisal.vw_AppraisalValidationContext</c>.
///
/// Rules are expressed in <see cref="Parameters.Rules"/> and stored in
/// <c>ActivityProcessConfiguration.ParametersJson</c> — no redeploy needed to add or
/// change rules. The available field keys are whitelisted in
/// <see cref="AppraisalFieldRegistry"/>.
/// </summary>
public class ValidateAppraisalFieldsStep(
    ISqlConnectionFactory connectionFactory,
    IPredicateEvaluator predicateEvaluator,
    ILogger<ValidateAppraisalFieldsStep> logger) : IActivityProcessStep
{
    // ── Parameter types ───────────────────────────────────────────────────

    /// <summary>Comparison operators for simple per-field rules.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RuleOperator
    {
        Required,
        Equals,
        NotEquals,
        GreaterThan,
        GreaterOrEqual,
        LessThan,
        LessOrEqual,
    }

    /// <summary>How multiple rules are combined.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RuleMode
    {
        /// <summary>All rules must pass (default).</summary>
        AllMustPass,
    }

    /// <summary>
    /// A single validation rule. Use either the simple field form (FieldKey + Op + Value)
    /// or the Jint expression escape-hatch (Expression only) — not both.
    /// </summary>
    public sealed record Rule
    {
        /// <summary>Registry key of the field to check (simple rules). See GET /api/workflow/admin/validation-fields for the list.</summary>
        [Description("Registry key of the field to evaluate (e.g. 'facilityLimit', 'status'). See GET /api/workflow/admin/validation-fields.")]
        public string? FieldKey { get; init; }

        /// <summary>Comparison operator for simple field rules (Required, Equals, NotEquals, GreaterThan, GreaterOrEqual, LessThan, LessOrEqual).</summary>
        [Description("Comparison operator: Required | Equals | NotEquals | GreaterThan | GreaterOrEqual | LessThan | LessOrEqual.")]
        public RuleOperator? Op { get; init; }

        /// <summary>Expected comparison value for Equals/NotEquals/numeric operators. Leave null for Required.</summary>
        [Description("Expected value string (for Equals/NotEquals/numeric operators). Omit for Required.")]
        public string? Value { get; init; }

        /// <summary>Jint boolean expression giving full access to appraisal.*, workflow.variables.*, and user.*. Mutually exclusive with FieldKey/Op.</summary>
        [Description("Jint JS expression (must return boolean). Has access to appraisal.*, workflow.variables.*, activity.*, user.*. Mutually exclusive with FieldKey/Op.")]
        public string? Expression { get; init; }

        /// <summary>Human-readable failure message shown to the user when this rule fails.</summary>
        [Description("Failure message shown when this rule is not satisfied. Falls back to a default if omitted.")]
        public string? Message { get; init; }
    }

    /// <summary>
    /// Parameters for the ValidateAppraisalFields step.
    /// Contains an ordered list of rules evaluated left-to-right; all failures are collected.
    /// </summary>
    public sealed record Parameters
    {
        /// <summary>Ordered list of rules to evaluate. All failures are collected before returning.</summary>
        [Description("Ordered list of field rules or expression rules to evaluate. All failures are collected and reported together.")]
        public List<Rule> Rules { get; init; } = new();

        /// <summary>How multiple rules are combined. Default: AllMustPass (all rules must pass).</summary>
        [Description("Rule combination mode. Currently only 'AllMustPass' is supported.")]
        public RuleMode Mode { get; init; } = RuleMode.AllMustPass;
    }

    // ── Descriptor ────────────────────────────────────────────────────────

    public StepDescriptor Descriptor { get; } = StepDescriptor.For<Parameters>(
        name: "ValidateAppraisalFields",
        displayName: "Validate Appraisal Fields",
        kind: StepKind.Validation,
        description: "Checks appraisal-level scalar fields against admin-configured rules (field comparisons or Jint expressions). All failures are collected and returned together.",
        exampleParametersJson: """
            {
              "rules": [
                {
                  "fieldKey": "facilityLimit",
                  "op": "Required",
                  "message": "Facility limit must be set before completing this activity."
                },
                {
                  "fieldKey": "appraisedValue",
                  "op": "GreaterThan",
                  "value": "0",
                  "message": "Appraised value must be greater than zero."
                },
                {
                  "expression": "appraisal.propertyCount > 0",
                  "message": "At least one property must be registered."
                }
              ],
              "mode": "AllMustPass"
            }
            """);

    // ── Execution ─────────────────────────────────────────────────────────

    public async Task<ProcessStepResult> ExecuteAsync(ProcessStepContext ctx, CancellationToken ct)
    {
        if (ctx.AppraisalId is null)
            return ProcessStepResult.Fail(
                "APPRAISAL_NOT_CREATED",
                "Appraisal has not been created yet; validation context is unavailable.");

        var p = ctx.GetParameters<Parameters>();
        if (p.Rules.Count == 0)
            return ProcessStepResult.Pass();

        try
        {
            using var connection = connectionFactory.GetOpenConnection();

            // Load the validation-context row once. Use the dynamic/DapperRow path (same as
            // ValidatePropertyMandatoryFieldsStep): the resulting IDictionary is CASE-INSENSITIVE,
            // so registry column lookups are robust to column-name casing — unlike a plain
            // Dictionary<string,object> which is case-sensitive and silently misses every column.
            var queried = await connection.QueryAsync(
                "SELECT * FROM appraisal.vw_AppraisalValidationContext WHERE AppraisalId = @AppraisalId",
                new { AppraisalId = ctx.AppraisalId.Value });

            if (queried.FirstOrDefault() is not IDictionary<string, object> row)
            {
                logger.LogWarning(
                    "Appraisal {AppraisalId} not found in vw_AppraisalValidationContext",
                    ctx.AppraisalId);
                return ProcessStepResult.Fail(
                    "APPRAISAL_NOT_FOUND",
                    "Appraisal validation context row not found.");
            }

            // Map registry keys → values.
            var fieldData = BuildFieldData(row);

            // Populate AppraisalData on the context so Jint can access it via "appraisal.*".
            var ctxWithData = ctx with { AppraisalData = fieldData };

            // Evaluate rules, collecting all failures.
            var failures = new List<string>();

            foreach (var rule in p.Rules)
            {
                string? failureMessage = EvaluateRule(rule, fieldData, ctxWithData);
                if (failureMessage is not null)
                    failures.Add(failureMessage);
            }

            if (failures.Count > 0)
            {
                var combined = string.Join(" ", failures);
                logger.LogWarning(
                    "ValidateAppraisalFields failed for appraisal {AppraisalId}: {Message}",
                    ctx.AppraisalId, combined);
                return ProcessStepResult.Fail("APPRAISAL_FIELDS_INVALID", combined);
            }

            return ProcessStepResult.Pass();
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to validate appraisal fields for appraisal {AppraisalId}", ctx.AppraisalId);
            return ProcessStepResult.Error(ex);
        }
    }

    // ── Rule evaluation ───────────────────────────────────────────────────

    private string? EvaluateRule(
        Rule rule,
        IReadOnlyDictionary<string, object?> fieldData,
        ProcessStepContext ctxWithData)
    {
        // Expression escape-hatch
        if (!string.IsNullOrWhiteSpace(rule.Expression))
        {
            try
            {
                var passed = predicateEvaluator.EvaluateExpression(rule.Expression, ctxWithData);
                if (!passed)
                    return rule.Message ?? $"Expression rule failed: {rule.Expression}";
                return null;
            }
            catch (PredicateEvaluationException pex)
            {
                return $"Expression rule error: {pex.Message}";
            }
        }

        // Simple field rule
        if (string.IsNullOrWhiteSpace(rule.FieldKey))
            return "Rule is missing both FieldKey and Expression.";

        var fieldDef = AppraisalFieldRegistry.Resolve(rule.FieldKey);
        if (fieldDef is null)
            return $"Unknown field key '{rule.FieldKey}'.";

        fieldData.TryGetValue(rule.FieldKey, out var rawValue);

        var op = rule.Op ?? RuleOperator.Required;

        return op switch
        {
            RuleOperator.Required => EvaluateRequired(rawValue, rule, fieldDef),
            RuleOperator.Equals => EvaluateComparison(rawValue, rule, fieldDef, op),
            RuleOperator.NotEquals => EvaluateComparison(rawValue, rule, fieldDef, op),
            RuleOperator.GreaterThan => EvaluateComparison(rawValue, rule, fieldDef, op),
            RuleOperator.GreaterOrEqual => EvaluateComparison(rawValue, rule, fieldDef, op),
            RuleOperator.LessThan => EvaluateComparison(rawValue, rule, fieldDef, op),
            RuleOperator.LessOrEqual => EvaluateComparison(rawValue, rule, fieldDef, op),
            _ => $"Unknown operator '{op}'."
        };
    }

    private static string? EvaluateRequired(object? rawValue, Rule rule, FieldDef fieldDef)
    {
        var isPresent = rawValue switch
        {
            null => false,
            string s => !string.IsNullOrWhiteSpace(s),
            bool b => true,         // false is a valid boolean "present" value
            _ => true
        };

        return isPresent
            ? null
            : (rule.Message ?? $"{fieldDef.DisplayName} is required.");
    }

    private static string? EvaluateComparison(
        object? rawValue,
        Rule rule,
        FieldDef fieldDef,
        RuleOperator op)
    {
        // String comparisons
        if (fieldDef.DataType == "string")
        {
            var actual = rawValue?.ToString() ?? string.Empty;
            var expected = rule.Value ?? string.Empty;

            bool passed = op switch
            {
                RuleOperator.Equals => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
                RuleOperator.NotEquals => !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
                _ => true  // Ordered comparisons on strings are not meaningful; skip.
            };

            return passed ? null : (rule.Message ?? DefaultComparisonMessage(fieldDef, op, rule.Value));
        }

        // Boolean comparisons
        if (fieldDef.DataType == "boolean")
        {
            var actual = ToBoolean(rawValue);
            var expected = ToBoolean(rule.Value);   // accepts "true"/"True" or "1"

            bool passed = op switch
            {
                RuleOperator.Equals => actual == expected,
                RuleOperator.NotEquals => actual != expected,
                _ => true
            };

            return passed ? null : (rule.Message ?? DefaultComparisonMessage(fieldDef, op, rule.Value));
        }

        // Numeric comparisons
        if (fieldDef.DataType == "number")
        {
            if (!TryToDecimal(rawValue, out var actual))
                return rule.Message ?? $"{fieldDef.DisplayName}: value is not a number.";

            if (!TryToDecimal(rule.Value, out var expected))
                return $"Rule configuration error: '{rule.Value}' is not a valid number for {fieldDef.DisplayName}.";

            bool passed = op switch
            {
                RuleOperator.Equals => actual == expected,
                RuleOperator.NotEquals => actual != expected,
                RuleOperator.GreaterThan => actual > expected,
                RuleOperator.GreaterOrEqual => actual >= expected,
                RuleOperator.LessThan => actual < expected,
                RuleOperator.LessOrEqual => actual <= expected,
                _ => true
            };

            return passed ? null : (rule.Message ?? DefaultComparisonMessage(fieldDef, op, rule.Value));
        }

        return null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a fieldKey → value dictionary from the raw Dapper row using the registry.
    /// </summary>
    private static IReadOnlyDictionary<string, object?> BuildFieldData(
        IDictionary<string, object> row)
    {
        var result = new Dictionary<string, object?>(AppraisalFieldRegistry.Fields.Count,
            StringComparer.OrdinalIgnoreCase);

        foreach (var field in AppraisalFieldRegistry.Fields)
        {
            if (row.TryGetValue(field.Column, out var val))
                result[field.Key] = val == DBNull.Value ? null : val;
            else
                result[field.Key] = null;
        }

        return result;
    }

    /// <summary>
    /// Interprets a value as a boolean. Handles every numeric boxing SQL Server / Dapper can
    /// produce for a 0/1 flag column (int, long, short, byte, decimal/float/double, bit→bool)
    /// plus string forms ("1"/"true"/"yes"). Anything else (incl. null) is false.
    /// This tolerance matters because the validation-context view exposes flags as
    /// `CASE … THEN 1 ELSE 0 END`, and the exact CLR type depends on the column/driver.
    /// </summary>
    private static bool ToBoolean(object? value) =>
        value switch
        {
            null => false,
            bool b => b,
            byte by => by != 0,
            short s => s != 0,
            int i => i != 0,
            long l => l != 0,
            decimal d => d != 0,
            double db => db != 0,
            float f => f != 0,
            string str => str.Trim() is var t
                && (t == "1"
                    || string.Equals(t, "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(t, "yes", StringComparison.OrdinalIgnoreCase)),
            _ => false
        };

    private static bool TryToDecimal(object? value, out decimal result)
    {
        result = 0;
        if (value is null) return false;
        return decimal.TryParse(value.ToString(), out result);
    }

    private static string DefaultComparisonMessage(FieldDef fieldDef, RuleOperator op, string? expected) =>
        $"{fieldDef.DisplayName} must be {op} {expected}.";
}
