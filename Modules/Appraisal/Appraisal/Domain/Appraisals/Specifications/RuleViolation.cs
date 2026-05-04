namespace Appraisal.Domain.Appraisals.Specifications;

/// <summary>
/// A single business rule violation produced by an <see cref="IPricingAnalysisPrecondition"/>.
/// Code is a stable, locale-independent identifier that the React client maps to a translated label.
/// PropertyId is set when the violation is scoped to a single property within the group.
/// </summary>
public sealed record RuleViolation(
    string Code,
    string Message,
    Guid? PropertyId = null
);
