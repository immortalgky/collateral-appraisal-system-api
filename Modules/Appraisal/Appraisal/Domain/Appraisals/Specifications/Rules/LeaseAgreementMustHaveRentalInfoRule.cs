namespace Appraisal.Domain.Appraisals.Specifications.Rules;

/// <summary>
/// Rule 3 — any property under a lease agreement (LSL, LSB, LS) must have
/// a RentalInfo row AND at least one schedule entry (the rental table).
/// </summary>
public sealed class LeaseAgreementMustHaveRentalInfoRule : IPricingAnalysisPrecondition
{
    public IEnumerable<RuleViolation> Check(ReadinessSnapshot snapshot)
    {
        foreach (var property in snapshot.Properties)
        {
            var type = PropertyType.FromString(property.PropertyType);
            if (!type.IsLeaseAgreement) continue;

            if (!property.HasRentalInfo)
            {
                yield return new RuleViolation(
                    Code: ViolationCodes.RentalInfoRequired,
                    Message: "Lease agreement property requires rental information before pricing analysis can start.",
                    PropertyId: property.PropertyId);
                continue;
            }

            if (!property.HasRentalSchedule)
            {
                yield return new RuleViolation(
                    Code: ViolationCodes.RentalScheduleRequired,
                    Message: "Lease agreement property requires at least one rental schedule entry.",
                    PropertyId: property.PropertyId);
            }
        }
    }
}
