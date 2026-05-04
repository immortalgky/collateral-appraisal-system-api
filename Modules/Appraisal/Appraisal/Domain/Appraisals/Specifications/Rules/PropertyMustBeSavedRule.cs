namespace Appraisal.Domain.Appraisals.Specifications.Rules;

/// <summary>
/// Rule 4 — every property in the group must have status = Saved.
/// Properties marked Draft are still being captured and cannot be priced.
/// </summary>
public sealed class PropertyMustBeSavedRule : IPricingAnalysisPrecondition
{
    public IEnumerable<RuleViolation> Check(ReadinessSnapshot snapshot)
    {
        foreach (var property in snapshot.Properties)
        {
            if (property.Status != PropertyStatus.Saved.Code)
            {
                yield return new RuleViolation(
                    Code: ViolationCodes.PropertyNotSaved,
                    Message: "Property must be in Saved status before pricing analysis can start.",
                    PropertyId: property.PropertyId);
            }
        }
    }
}
