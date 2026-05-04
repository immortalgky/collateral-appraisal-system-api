namespace Appraisal.Domain.Appraisals.Specifications;

/// <summary>
/// Domain service that composes every registered <see cref="IPricingAnalysisPrecondition"/>
/// and reports whether a property group can enter pricing analysis. Used by both the
/// write-side gate (CreatePricingAnalysis / StartPricingAnalysis) and the read-side
/// readiness projection on GetPropertyGroupById — single source of truth, no rule drift.
/// </summary>
public sealed class PricingAnalysisReadinessChecker
{
    private readonly IReadOnlyList<IPricingAnalysisPrecondition> _rules;

    public PricingAnalysisReadinessChecker(IEnumerable<IPricingAnalysisPrecondition> rules)
    {
        _rules = rules.ToList();
    }

    public ReadinessResult Evaluate(ReadinessSnapshot snapshot)
    {
        var violations = _rules
            .SelectMany(r => r.Check(snapshot))
            .ToList();

        return ReadinessResult.From(violations);
    }
}
