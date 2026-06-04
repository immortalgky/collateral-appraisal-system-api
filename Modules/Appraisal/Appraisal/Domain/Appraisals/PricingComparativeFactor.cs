namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Step 1 factor selections for comparative analysis.
/// Records which factors are selected for comparison and which are used for scoring.
/// </summary>
public class PricingComparativeFactor : Entity<Guid>
{
    public Guid PricingMethodId { get; private set; }
    public Guid FactorId { get; private set; } // FK → MarketComparableFactor
    public int DisplaySequence { get; private set; }
    public bool IsSelectedForScoring { get; private set; } // Marks factors used in Step 2
    public string? Remarks { get; private set; }

    /// <summary>
    /// Persisted subject-column value for reference analyses where no backing property exists
    /// (e.g. room-type or profit-rent references). Null for normal property-group analyses.
    /// </summary>
    public string? CollateralValue { get; private set; }

    private PricingComparativeFactor() { }

    public static PricingComparativeFactor Create(
        Guid pricingMethodId,
        Guid factorId,
        int displaySequence,
        bool isSelectedForScoring = false,
        string? remarks = null,
        string? collateralValue = null)
    {
        return new PricingComparativeFactor
        {
            Id = Guid.CreateVersion7(),
            PricingMethodId = pricingMethodId,
            FactorId = factorId,
            DisplaySequence = displaySequence,
            IsSelectedForScoring = isSelectedForScoring,
            Remarks = remarks,
            CollateralValue = collateralValue
        };
    }

    /// <summary>Deep-clone for CI carry-forward.</summary>
    public static PricingComparativeFactor CloneForMethod(PricingComparativeFactor source, Guid newMethodId)
    {
        return new PricingComparativeFactor
        {
            Id = Guid.CreateVersion7(),
            PricingMethodId = newMethodId,
            FactorId = source.FactorId,
            DisplaySequence = source.DisplaySequence,
            IsSelectedForScoring = source.IsSelectedForScoring,
            Remarks = source.Remarks,
            CollateralValue = source.CollateralValue
        };
    }

    public void SetSelectedForScoring(bool selected)
    {
        IsSelectedForScoring = selected;
    }

    public void UpdateSequence(int sequence)
    {
        DisplaySequence = sequence;
    }

    public void SetRemarks(string? remarks)
    {
        Remarks = remarks;
    }

    /// <summary>
    /// Updates all properties at once.
    /// </summary>
    public void Update(int displaySequence, bool isSelectedForScoring, string? remarks, string? collateralValue = null)
    {
        DisplaySequence = displaySequence;
        IsSelectedForScoring = isSelectedForScoring;
        Remarks = remarks;
        CollateralValue = collateralValue;
    }
}
