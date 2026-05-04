namespace Appraisal.Domain.Appraisals.Specifications;

/// <summary>
/// Aggregated outcome of running every <see cref="IPricingAnalysisPrecondition"/>
/// against a <see cref="ReadinessSnapshot"/>.
/// </summary>
public sealed record ReadinessResult(
    bool IsReady,
    IReadOnlyList<RuleViolation> Violations)
{
    public static ReadinessResult From(IReadOnlyList<RuleViolation> violations)
        => new(violations.Count == 0, violations);
}
