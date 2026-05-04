namespace Appraisal.Domain.Appraisals.Specifications.Rules;

/// <summary>
/// Rule 2 — any property whose type implies a building component (B, LB, LSB, LS)
/// must have its BuildingAppraisalDetail row populated.
/// </summary>
public sealed class BuildingMustHaveBuildingDetailRule : IPricingAnalysisPrecondition
{
    public IEnumerable<RuleViolation> Check(ReadinessSnapshot snapshot)
    {
        foreach (var property in snapshot.Properties)
        {
            var type = PropertyType.FromString(property.PropertyType);
            if (type.HasBuildingDetail && !property.HasBuildingDetail)
            {
                yield return new RuleViolation(
                    Code: ViolationCodes.BuildingDetailRequired,
                    Message: $"Property requires building detail before pricing analysis can start.",
                    PropertyId: property.PropertyId);
            }
        }
    }
}
