namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Links an appraisal to market comparables used in valuation.
/// </summary>
public class AppraisalComparable : Entity<Guid>
{
    private readonly List<ComparableAdjustment> _adjustments = [];
    public IReadOnlyList<ComparableAdjustment> Adjustments => _adjustments.AsReadOnly();

    // Core Properties
    public Guid AppraisalId { get; private set; }
    public Guid MarketComparableId { get; private set; }

    // Sequence & Weight
    public int SequenceNumber { get; private set; }
    public decimal Weight { get; private set; } // Percentage weight in final value

    // Values
    public decimal OriginalPricePerUnit { get; private set; }
    public decimal AdjustedPricePerUnit { get; private set; }
    public decimal TotalAdjustmentPct { get; private set; }
    public decimal WeightedValue { get; private set; }

    // Notes
    public string? SelectionReason { get; private set; }
    public string? Notes { get; private set; }

    private AppraisalComparable()
    {
    }

    public static AppraisalComparable Create(
        Guid appraisalId,
        Guid marketComparableId,
        int sequenceNumber,
        decimal originalPricePerUnit,
        decimal weight = 0)
    {
        return new AppraisalComparable
        {
            Id = Guid.NewGuid(),
            AppraisalId = appraisalId,
            MarketComparableId = marketComparableId,
            SequenceNumber = sequenceNumber,
            OriginalPricePerUnit = originalPricePerUnit,
            AdjustedPricePerUnit = originalPricePerUnit,
            Weight = weight,
            TotalAdjustmentPct = 0
        };
    }

    public ComparableAdjustment AddAdjustment(
        string adjustmentCategory,
        string adjustmentType,
        decimal adjustmentPercent,
        string? justification = null)
    {
        var adjustment = ComparableAdjustment.Create(
            Id, adjustmentCategory, adjustmentType, adjustmentPercent, justification);
        _adjustments.Add(adjustment);
        RecalculateAdjustments();
        return adjustment;
    }

    public void SetWeight(decimal weight)
    {
        if (weight < 0 || weight > 100)
            throw new ArgumentException("Weight must be between 0 and 100");
        Weight = weight;
        RecalculateWeightedValue();
    }

    public void SetSelectionReason(string reason)
    {
        SelectionReason = reason;
    }

    private void RecalculateAdjustments()
    {
        TotalAdjustmentPct = _adjustments.Sum(a => a.AdjustmentPercent);
        AdjustedPricePerUnit = OriginalPricePerUnit * (1 + TotalAdjustmentPct / 100);
        RecalculateWeightedValue();
    }

    private void RecalculateWeightedValue()
    {
        WeightedValue = AdjustedPricePerUnit * Weight / 100;
    }
}