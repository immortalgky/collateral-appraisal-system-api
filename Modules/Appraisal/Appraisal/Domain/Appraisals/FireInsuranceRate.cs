namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Fire-insurance coverage rate per building condition: the recommended coverage for a collateral
/// is RatePerSqm × usable area.
/// </summary>
/// <remarks>
/// Reference data owned by the Appraisal module — every consumer (condo property detail, block
/// project models, unit-price calculation) lives here, which is why it no longer sits behind a
/// cross-module query in Parameter.
///
/// <para><see cref="Code"/> is the key everything stores and joins on. <see cref="Condition"/> is
/// kept as the human-readable name of the same row — useful when reading data by hand, and the
/// fallback label the UI shows when the 'FireInsuranceCondition' parameter group has no matching
/// description — but nothing joins on it.</para>
/// </remarks>
public class FireInsuranceRate
{
    /// <summary>Rate code, matches the parameter.Parameters group 'FireInsuranceCondition', codes '01'-'12'.</summary>
    public string Code { get; private set; } = null!;

    /// <summary>Human-readable name of the condition, e.g. "GreaterThan8Floors". Not a join key.</summary>
    public string Condition { get; private set; } = null!;

    /// <summary>Property kind the condition applies to: "Condo" or "LandAndBuilding".</summary>
    public string PropertyKind { get; private set; } = null!;

    /// <summary>Fire-insurance coverage rate, in Baht per sq.m. of usable area.</summary>
    public decimal RatePerSqm { get; private set; }

    public int DisplaySeq { get; private set; }

    private FireInsuranceRate()
    {
        // For EF Core
    }
}
