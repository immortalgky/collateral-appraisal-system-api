using Appraisal.Domain.Appraisals.Specifications;

namespace Appraisal.Domain.Appraisals.Exceptions;

/// <summary>
/// Thrown when an attempt is made to create or start a pricing analysis on a property
/// group that fails one or more <see cref="IPricingAnalysisPrecondition"/>. Mapped to
/// HTTP 422 Unprocessable Entity with the violation list surfaced under
/// <c>extensions.violations</c> in the ProblemDetails payload.
/// </summary>
public class PricingAnalysisNotReadyException : UnprocessableEntityException
{
    public PricingAnalysisNotReadyException(IReadOnlyList<RuleViolation> violations)
        : base(
            "Pricing analysis cannot start: one or more readiness rules failed.",
            violations.Select(v => new RuleViolationInfo(v.Code, v.Message, v.PropertyId)).ToList())
    {
    }
}
