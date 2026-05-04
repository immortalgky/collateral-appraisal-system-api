namespace Appraisal.Domain.Appraisals.Specifications.Rules;

/// <summary>
/// Rule 1 — every appraisal entering pricing analysis must have at least one
/// market survey (AppraisalComparable) attached.
/// </summary>
public sealed class MarketSurveyRequiredRule : IPricingAnalysisPrecondition
{
    public IEnumerable<RuleViolation> Check(ReadinessSnapshot snapshot)
    {
        if (snapshot.MarketSurveyCount < 1)
        {
            yield return new RuleViolation(
                Code: ViolationCodes.MarketSurveyRequired,
                Message: "At least one market survey is required for the appraisal before pricing analysis can start.");
        }
    }
}
