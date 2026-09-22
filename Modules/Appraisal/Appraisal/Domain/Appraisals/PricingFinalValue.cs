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
/// <c>PricingAnalysisMethod.UnitType</c> to tell which.</item>
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
