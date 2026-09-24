namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Final values per pricing method.
/// </summary>
/// <remarks>
/// Three columns, three distinct jobs — read them as:
/// <list type="bullet">
/// <item><see cref="FinalValueOverride"/> — the figure the appraiser adjusted by hand. Its unit
/// follows the method: a rate per Sq.Wa / Sq.M when the method prices by area, a whole-property
/// lump sum when it does not (machinery, hypothesis, income, leasehold, profit rent). Read
/// <see cref="FinalValueUnitType"/> on this row to tell which.</item>
/// <item><see cref="FinalValue"/> — what the system worked out, with its own rounding applied.</item>
/// <item><see cref="IndicatedValue"/> — what the appraiser typed over the total; null means they did not.</item>
/// </list>
/// The price to use is therefore <c>IndicatedValue ?? FinalValue</c>, the same shape as the
/// property form's Building Cost Value (<c>FinalCostValueOverride ?? computed</c>).
/// <para>
/// There used to be a second column holding the figure before rounding. Nothing downstream read
/// it — not the book, not the collateral master, not the AS400 exports — and carrying both made it
/// unclear which one to price against, so it is gone.
/// </para>
/// </remarks>
public class PricingFinalValue : Entity<Guid>
{
    public Guid PricingMethodId { get; private set; }

    // Final Value
    /// <summary>The system's computed figure, already rounded by the method's own rule.</summary>
    public decimal FinalValue { get; private set; }

    /// <summary>
    /// The appraiser's own figure, overriding what the calculation produced. Deliberately not named
    /// for a unit: it is a per-area rate on some methods and a lump sum on others.
    /// </summary>
    public decimal? FinalValueOverride { get; private set; }

    /// <summary>
    /// What <see cref="FinalValue"/> and <see cref="FinalValueOverride"/> are measured in, stamped
    /// when the figure is written: <c>PerSqWa</c> / <c>PerSqm</c> for a per-area rate, <c>PerUnit</c>
    /// for a whole-property lump sum (the <c>PricingUnit</c> vocabulary).
    /// <para>
    /// It exists so this row answers the question itself. The figures here are a per-area rate on
    /// some methods and a whole-property total on others, and until now the only way to tell was to
    /// join to <c>PricingAnalysisMethod.UnitType</c> — which flipping the method's calc mode nulls
    /// (<c>SetCalcMode</c> → <c>ClearValue</c>) while leaving these figures standing.
    /// </para>
    /// <para>
    /// Be precise about what it does NOT buy, so nobody relies on it for the wrong thing:
    /// <list type="bullet">
    /// <item>It is no defence against the method's unit changing. A re-derivation from the
    /// comparables goes through <c>ResolvePriceUnit</c>, which never returns null, so the method's
    /// own unit is present and every consumer that reads it first will use it.</item>
    /// <item>The three save handlers that derive LandValue read
    /// <c>method.UnitType ?? (IncludeLandArea ? this : null)</c>, and the one operation that nulls
    /// <c>UnitType</c> also excludes land area — so as things stand they never actually fall through
    /// to this column. It is written for readers, not yet consulted by those writers.</item>
    /// </list>
    /// </para>
    /// <para>
    /// Null means lump sum — the same reading <c>PricingUnit.IsPerUnitRate(null) == false</c> gives,
    /// so rows written before this column need no special case from any consumer.
    /// </para>
    /// </summary>
    public string? FinalValueUnitType { get; private set; }

    // Land Area Inclusion
    public bool IncludeLandArea { get; private set; } = true;
    public decimal? LandArea { get; private set; }
    public decimal? LandValue { get; private set; }   // user-edited land price

    // Building Value (if applicable)
    public bool HasBuildingValue { get; private set; }
    public decimal? BuildingValue { get; private set; }
    public decimal? IndicatedValue { get; private set; } // user-edited final total (HasBuildingValue only)

    private PricingFinalValue()
    {
    }

    public static PricingFinalValue Create(
        Guid pricingMethodId,
        decimal finalValue)
    {
        return new PricingFinalValue
        {
            Id = Guid.CreateVersion7(),
            PricingMethodId = pricingMethodId,
            FinalValue = finalValue,
            IncludeLandArea = true,
            HasBuildingValue = false
        };
    }

    /// <summary>Deep-clone for CI carry-forward.</summary>
    public static PricingFinalValue CloneForMethod(PricingFinalValue source, Guid newMethodId)
    {
        return new PricingFinalValue
        {
            Id = Guid.CreateVersion7(),
            PricingMethodId = newMethodId,
            FinalValue = source.FinalValue,
            FinalValueOverride = source.FinalValueOverride,
            FinalValueUnitType = source.FinalValueUnitType,
            IncludeLandArea = source.IncludeLandArea,
            LandArea = source.LandArea,
            LandValue = source.LandValue,
            HasBuildingValue = source.HasBuildingValue,
            BuildingValue = source.BuildingValue,
            IndicatedValue = source.IndicatedValue
        };
    }

    public void SetLandAreaValues(decimal landArea, decimal landValue)
    {
        IncludeLandArea = true;
        LandArea = landArea;
        LandValue = landValue;
    }

    public void ExcludeLandArea()
    {
        IncludeLandArea = false;
        LandArea = null;
        LandValue = null;
    }

    /// <summary>
    /// Drops the per-component land figures without touching <see cref="IncludeLandArea"/>, which is
    /// the appraiser's own answer to "does this method price land at all" and is not ours to flip.
    /// For a method that values the collateral as one lump there is no land figure to record, but the
    /// appraiser's flag still means what they set it to.
    /// <para>
    /// It clears rather than merely skipping because a row saved under the old rule already carries a
    /// land value, and money left behind is worse than money never written: the book, LOS, MIS and the
    /// AS400 regulatory file read this column with no idea which approach produced it.
    /// </para>
    /// </summary>
    public void ClearLandAreaValues()
    {
        LandArea = null;
        LandValue = null;
    }

    /// <summary>
    /// Drops the appraiser's typed-over total. Part of switching a method back to system
    /// calculation: that says "compute this from the comparables", and an IndicatedValue left behind
    /// does the opposite — SyncMethodValueWithIndicatedValue pushes it back over MethodValue on the
    /// next save, pinning the method to a figure the appraiser just asked it to stop using.
    /// </summary>
    public void ClearIndicatedValue()
    {
        IndicatedValue = null;
    }

    /// <summary>
    /// Records that the appraiser answered "yes, this method prices land" without deriving any
    /// figures from it. <see cref="SetLandAreaValues"/> is the only other writer that turns the flag
    /// back on, and a method that records no land never reaches it — so unticking the box and
    /// re-ticking it left the flag stuck at false, permanently dropping the พื้นที่ and ราคาต่อหน่วย
    /// columns from that group's row in the summary book.
    /// </summary>
    public void MarkLandAreaIncluded()
    {
        IncludeLandArea = true;
    }

    public void SetBuildingValue(decimal buildingValue)
    {
        HasBuildingValue = true;
        BuildingValue = buildingValue;
    }

    /// <summary>
    /// Records the system's computed figure. Takes one value on purpose: the signature used to take
    /// the raw and rounded figures as two same-typed decimals, so a caller that passed them the
    /// wrong way round still compiled.
    /// </summary>
    public void UpdateFinalValue(decimal finalValue)
    {
        FinalValue = finalValue;
    }

    public void SetFinalValueOverride(decimal? value)
    {
        FinalValueOverride = value;
    }

    /// <summary>
    /// Stamps the unit the figures on this row are measured in. Called by the owning
    /// <see cref="PricingAnalysisMethod"/> whenever it records a value or attaches this row, so no
    /// save handler has to remember to do it.
    /// </summary>
    public void SetFinalValueUnitType(string? unitType)
    {
        FinalValueUnitType = unitType;
    }

    /// <summary>
    /// Persists the user-rounded appraisal price independent of the building-cost toggle.
    /// The same rounded number applies to land-only cost (unit 01/02), machinery (unit 03),
    /// market approach, and the with-building-cost case.
    /// </summary>
    public void SetIndicatedValue(decimal? indicatedValue)
    {
        IndicatedValue = indicatedValue;
    }

    public void ClearBuildingValue()
    {
        HasBuildingValue = false;
        BuildingValue = null;
    }

}
