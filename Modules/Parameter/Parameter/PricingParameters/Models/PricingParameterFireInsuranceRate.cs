namespace Parameter.PricingParameters.Models;

/// <summary>
/// Fire-insurance coverage rate per building condition, used to derive the recommended
/// insurance coverage amount for a collateral (RatePerSqm × usable area).
/// </summary>
/// <remarks>
/// Both <see cref="Code"/> and <see cref="Condition"/> are kept: <see cref="Condition"/> is the
/// same string value already persisted on <c>appraisal.ProjectModels.FireInsuranceCondition</c>,
/// so lookups by condition require no data migration on the Appraisal side; <see cref="Code"/>
/// reconciles the row with the seeded 'FireInsuranceCondition' group in parameter.Parameters.
/// </remarks>
public class PricingParameterFireInsuranceRate
{
    /// <summary>Parameter code, matches parameter.Parameters group 'FireInsuranceCondition', codes '01'-'12'.</summary>
    public string Code { get; private set; } = null!;

    /// <summary>Condition key, matches the value stored in appraisal.ProjectModels.FireInsuranceCondition.</summary>
    public string Condition { get; private set; } = null!;

    /// <summary>Property kind the condition applies to: "Condo" or "LandAndBuilding".</summary>
    public string PropertyKind { get; private set; } = null!;

    /// <summary>Fire-insurance coverage rate, in Baht per sq.m. of usable area.</summary>
    public decimal RatePerSqm { get; private set; }

    public int DisplaySeq { get; private set; }

    private PricingParameterFireInsuranceRate()
    {
        // For EF Core
    }
}
