namespace Appraisal.Domain.Services;

/// <summary>
/// Canonical pricing "final price unit" vocabulary.
/// Replaces the legacy MeasurementUnits numeric codes ('01'/'02'/'03') for the
/// pricing price-unit concept on both the per-comparable unit fields
/// (MarketComparable / PricingCalculation) and the method-level
/// PricingAnalysisMethod.UnitType.
///
/// Semantics:
///   PerSqWa / PerSqm → per-unit RATE (ValuePerUnit is meaningful; no rounding).
///   PerUnit          → whole-unit LUMPSUM (ValuePerUnit is null; floor to nearest 1,000).
///
/// Consumer rule: UnitType == PerUnit ⟺ lumpsum; UnitType ∈ {PerSqWa, PerSqm} ⟺ per-unit rate.
/// </summary>
internal static class PricingUnit
{
    public const string PerSqWa = "PerSqWa";
    public const string PerSqm = "PerSqm";
    public const string PerUnit = "PerUnit";

    /// <summary>
    /// True when the unit denotes a per-unit rate (land per Sq.Wa / building per Sq.M),
    /// as opposed to a whole-unit lumpsum.
    /// </summary>
    public static bool IsPerUnitRate(string? unit) => unit is PerSqWa or PerSqm;

    /// <summary>
    /// Maps a legacy MeasurementUnits code to the vocabulary. Backfill/compat only —
    /// at runtime the stored value is already the vocabulary string.
    /// '01' → PerSqWa, '02' → PerSqm, everything else (incl. '03') → PerUnit.
    /// </summary>
    public static string FromMeasurementCode(string? code) => code switch
    {
        "01" => PerSqWa,
        "02" => PerSqm,
        _ => PerUnit,
    };
}
