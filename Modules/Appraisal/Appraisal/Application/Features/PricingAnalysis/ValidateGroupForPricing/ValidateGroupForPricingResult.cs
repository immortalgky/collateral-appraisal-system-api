namespace Appraisal.Application.Features.PricingAnalysis.ValidateGroupForPricing;

/// <summary>
/// Outcome of a single validation rule. Mirrors the shape of the workflow pipeline's
/// ProcessStepResult (Passed / Failed) plus a Skipped state for rules that do not apply
/// to the group's property mix, so the front-end can render a familiar per-step checklist.
/// </summary>
public enum PricingValidationStatus
{
    Passed,
    Failed,
    Skipped
}

/// <summary>
/// A single validation step result.
/// <para><b>Key</b> — stable rule identifier (e.g. "MakerSurvey").</para>
/// <para><b>DisplayName</b> — human-readable label for the checklist UI.</para>
/// <para><b>Messages</b> — per-property failure detail; empty when passed/skipped.</para>
/// </summary>
public record PricingValidationStep(
    string Key,
    string DisplayName,
    PricingValidationStatus Status,
    IReadOnlyList<string> Messages
);

/// <summary>
/// Aggregate validation result. <see cref="Valid"/> is true only when no step failed.
/// </summary>
public record ValidateGroupForPricingResult(
    bool Valid,
    IReadOnlyList<PricingValidationStep> Steps
);
