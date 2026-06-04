using System.ComponentModel;
using Dapper;
using Microsoft.Extensions.Logging;
using Shared.Data;
using Workflow.Data.Entities;

namespace Workflow.Workflow.Pipeline.Steps;

/// <summary>
/// Per-property mandatory-field validation step. Reads one row per property from
/// <c>appraisal.vw_AppraisalPropertyValidationContext</c>, which exposes each
/// validatable field as a BIT presence-flag column (1 = present, 0 = missing).
///
/// Adding a new validatable field requires only a new view column — no C# redeploy.
/// The column name == the fieldKey used in <c>requiredByType</c> config.
///
/// Fail-safe: if a configured fieldKey is not a column in the view row (e.g. config typo
/// or a field added to the view but not yet deployed), it is skipped with a Warning log
/// and NEVER blocks completion. This prevents a config error from locking all users out.
/// </summary>
public class ValidatePropertyMandatoryFieldsStep(
    ISqlConnectionFactory connectionFactory,
    ILogger<ValidatePropertyMandatoryFieldsStep> logger) : IActivityProcessStep
{
    // ── Parameter types ───────────────────────────────────────────────────

    /// <summary>
    /// Parameters for the ValidatePropertyMandatoryFields step.
    /// Maps each supported property type code to the list of field keys that must be present.
    /// </summary>
    public sealed record Parameters
    {
        /// <summary>
        /// Property type code → list of required field keys.
        /// Each fieldKey must correspond to a column in vw_AppraisalPropertyValidationContext.
        /// Unknown field keys are silently skipped (fail-safe; see class summary).
        /// Properties whose type is not listed here are skipped entirely.
        /// </summary>
        [Description("Map of property type code → required field keys. Each key must be a column in vw_AppraisalPropertyValidationContext (e.g. TitleNumber, LandOffice, Province, District, SubDistrict). Properties of unlisted types are skipped.")]
        public Dictionary<string, List<string>> RequiredByType { get; init; } = new();
    }

    // ── Descriptor ────────────────────────────────────────────────────────

    public StepDescriptor Descriptor { get; } = StepDescriptor.For<Parameters>(
        name: "ValidatePropertyMandatoryFields",
        displayName: "Validate Property Mandatory Fields",
        kind: StepKind.Validation,
        description: "Checks that each appraisal property of a configured type has all required fields present. Reports missing fields per property with number and type.",
        exampleParametersJson: """
            {
              "requiredByType": {
                "L": ["TitleNumber", "LandOffice", "Province"],
                "U": ["TitleNumber", "Province", "District"]
              }
            }
            """);

    // ── Execution ─────────────────────────────────────────────────────────

    public async Task<ProcessStepResult> ExecuteAsync(ProcessStepContext ctx, CancellationToken ct)
    {
        if (ctx.AppraisalId is null)
            return ProcessStepResult.Fail(
                "APPRAISAL_NOT_CREATED",
                "Appraisal has not been created yet; property validation is unavailable.");

        var p = ctx.GetParameters<Parameters>();
        if (p.RequiredByType.Count == 0)
            return ProcessStepResult.Pass();

        try
        {
            using var connection = connectionFactory.GetOpenConnection();

            // Read one row per property as a dynamic dict. Column name == fieldKey.
            // Value 1 = field present; 0 = field missing.
            // Adding a new validatable field requires only a new view column — no redeploy.
            var rows = (await connection.QueryAsync(
                """
                SELECT *
                FROM appraisal.vw_AppraisalPropertyValidationContext
                WHERE AppraisalId = @AppraisalId
                ORDER BY SequenceNumber
                """,
                new { AppraisalId = ctx.AppraisalId.Value })).ToList();

            var violations = new List<string>();

            for (var i = 0; i < rows.Count; i++)
            {
                // Dapper dynamic rows expose via IDictionary<string,object>
                var row = (IDictionary<string, object>)rows[i];

                var propertyType = row.TryGetValue("PropertyType", out var pt) ? pt?.ToString() ?? "" : "";
                var sequenceNumber = row.TryGetValue("SequenceNumber", out var sn) ? Convert.ToInt32(sn) : i + 1;
                var displayNumber = sequenceNumber > 0 ? sequenceNumber : i + 1;

                // Case-insensitive lookup of the property type code.
                var matchedKey = p.RequiredByType.Keys
                    .FirstOrDefault(k => string.Equals(k, propertyType, StringComparison.OrdinalIgnoreCase));

                if (matchedKey is null)
                    continue; // Type not configured — skip.

                var requiredKeys = p.RequiredByType[matchedKey];
                var missingFields = new List<string>();

                foreach (var fieldKey in requiredKeys)
                {
                    if (!row.TryGetValue(fieldKey, out var rawValue))
                    {
                        // Fail-safe: the configured fieldKey is not a column in the view row.
                        // This can happen when: (a) the admin made a typo, (b) a new field was
                        // added to config before the view column was deployed.
                        // We skip rather than fail so a config error never locks users out.
                        logger.LogWarning(
                            "ValidatePropertyMandatoryFields: fieldKey '{FieldKey}' is not a column " +
                            "in vw_AppraisalPropertyValidationContext for appraisal {AppraisalId}. " +
                            "Skipping — check the view definition or correct the config.",
                            fieldKey, ctx.AppraisalId);
                        continue; // Fail-safe: skip, do NOT treat as missing.
                    }

                    if (!IsPresent(rawValue))
                        missingFields.Add(fieldKey);
                }

                if (missingFields.Count > 0)
                    violations.Add($"Property #{displayNumber} ({propertyType}): {string.Join(", ", missingFields)}.");
            }

            if (violations.Count > 0)
            {
                var message = string.Join(" ", violations);
                logger.LogWarning(
                    "Property mandatory fields missing for appraisal {AppraisalId}: {Message}",
                    ctx.AppraisalId, message);
                return ProcessStepResult.Fail("PROPERTY_FIELDS_MISSING", message);
            }

            return ProcessStepResult.Pass();
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to validate property mandatory fields for appraisal {AppraisalId}",
                ctx.AppraisalId);
            return ProcessStepResult.Error(ex);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Interprets a raw view column value as a presence flag.
    /// The view emits 0/1 integer values; 1 means present, 0 means missing.
    /// </summary>
    private static bool IsPresent(object? rawValue) =>
        rawValue switch
        {
            int i => i != 0,
            long l => l != 0,
            bool b => b,
            byte by => by != 0,
            _ => rawValue?.ToString() != "0"
        };
}
