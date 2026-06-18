namespace Appraisal.Application.Features.PricingAnalysis.ValidateGroupForPricing;

/// <summary>
/// Persistence-agnostic snapshot of one property, used to evaluate the pricing rules.
/// The handler builds these from the appraisal data; the evaluator stays pure and unit-testable.
/// </summary>
public record PricingValidationProperty(
    int SequenceNumber,
    string TypeCode,
    bool HasBuildingDetail,
    bool HasRentalSchedule
);

/// <summary>
/// Pure evaluation of the backend pricing-analysis pre-flight rules over a group's properties.
/// No EF / I/O — so the business rules can be unit-tested in isolation.
///
/// Note: the per-property "mandatory field" rule is validated on the front-end (it reuses the
/// shared field configs in fields.ts, including conditional requiredWhen rules) and is therefore
/// intentionally NOT part of this backend evaluator.
/// </summary>
public static class PricingGroupValidator
{
    /// <summary>Lease property type codes that require rental-schedule data.</summary>
    public static readonly string[] LeaseTypeCodes = ["LS", "LSL", "LSU", "LSB"];

    /// <summary>Building property type code.</summary>
    public static readonly string[] BuildingTypeCode = ["B", "LB", "LSB", "LS"];

    public static ValidateGroupForPricingResult Evaluate(
        IReadOnlyList<PricingValidationProperty> properties,
        int surveyCount)
    {
        // Rule 1: the group must contain at least one property.
        if (properties.Count == 0)
        {
            return new ValidateGroupForPricingResult(false,
            [
                Failed("HasProperties", "Properties in group",
                    ["The group has no properties. Add at least one property before pricing analysis."])
            ]);
        }

        var steps = new List<PricingValidationStep>
        {
            // Rule 2: at least one maker survey (market comparable).
            surveyCount > 0
                ? Passed("MarketSurvey", "Market survey")
                : Failed("MarketSurvey", "Market survey",
                    ["At least one survey (market comparable) is required for this appraisal."]),

            // Rule 3: each Building (B) property has a building-detail record.
            Evaluate("BuildingDetail", "Building detail",
                applicable: properties.Any(p => BuildingTypeCode.Contains(p.TypeCode)),
                missing: properties
                    .Where(p => BuildingTypeCode.Contains(p.TypeCode) && !p.HasBuildingDetail)
                    .Select(p => $"Property #{p.SequenceNumber} ({p.TypeCode}): building detail is missing.")
                    .ToList()),

            // Rule 4: each lease property has rental-schedule data.
            Evaluate("RentalSchedule", "Rental schedule",
                applicable: properties.Any(p => LeaseTypeCodes.Contains(p.TypeCode)),
                missing: properties
                    .Where(p => LeaseTypeCodes.Contains(p.TypeCode) && !p.HasRentalSchedule)
                    .Select(p => $"Property #{p.SequenceNumber} ({p.TypeCode}): rental schedule has no data.")
                    .ToList()),
        };

        var valid = steps.All(s => s.Status != PricingValidationStatus.Failed);
        return new ValidateGroupForPricingResult(valid, steps);
    }

    // ── Step factory helpers ──────────────────────────────────────────────────

    private static PricingValidationStep Passed(string key, string displayName) =>
        new(key, displayName, PricingValidationStatus.Passed, []);

    private static PricingValidationStep Failed(string key, string displayName, IReadOnlyList<string> messages) =>
        new(key, displayName, PricingValidationStatus.Failed, messages);

    /// <summary>
    /// Skipped when the rule does not apply to the group's property mix,
    /// Failed when there are violations, otherwise Passed.
    /// </summary>
    private static PricingValidationStep Evaluate(
        string key, string displayName, bool applicable, List<string> missing)
    {
        if (!applicable)
            return new(key, displayName, PricingValidationStatus.Skipped, []);

        return missing.Count > 0
            ? Failed(key, displayName, missing)
            : Passed(key, displayName);
    }
}
