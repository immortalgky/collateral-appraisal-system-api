namespace Appraisal.Domain.Appraisals.Hypothesis.CostItems;

/// <summary>
/// A single depreciation period row for a <see cref="HypothesisCostItem"/>
/// when its <see cref="HypothesisCostItem.DepreciationMethod"/> is "Period".
///
/// B06 contribution from this period = (ToYear - AtYear + 1) × DepreciationPerYear.
/// The parent sums all periods and caps at 100.
/// </summary>
public class HypothesisCostItemDepreciationPeriod : Entity<Guid>
{
    public Guid CostItemId { get; private set; }

    /// <summary>Display/sum order within the parent cost item's period list.</summary>
    public int Sequence { get; private set; }

    /// <summary>Start year of this depreciation band (inclusive, zero-based age in years).</summary>
    public int AtYear { get; private set; }

    /// <summary>End year of this depreciation band (inclusive). Must be &gt;= AtYear.</summary>
    public int ToYear { get; private set; }

    /// <summary>Annual depreciation rate for this band (%). Valid range: 0–100.</summary>
    public decimal DepreciationPerYear { get; private set; }

    private HypothesisCostItemDepreciationPeriod() { }

    internal static HypothesisCostItemDepreciationPeriod Create(
        Guid costItemId,
        int sequence,
        int atYear,
        int toYear,
        decimal depreciationPerYear)
    {
        return new HypothesisCostItemDepreciationPeriod
        {
            Id = Guid.CreateVersion7(),
            CostItemId = costItemId,
            Sequence = sequence,
            AtYear = atYear,
            ToYear = toYear,
            DepreciationPerYear = depreciationPerYear
        };
    }

    /// <summary>Deep-clone for CI carry-forward.</summary>
    internal static HypothesisCostItemDepreciationPeriod CloneForCostItem(
        HypothesisCostItemDepreciationPeriod source, Guid newCostItemId)
    {
        return new HypothesisCostItemDepreciationPeriod
        {
            Id = Guid.CreateVersion7(),
            CostItemId = newCostItemId,
            Sequence = source.Sequence,
            AtYear = source.AtYear,
            ToYear = source.ToYear,
            DepreciationPerYear = source.DepreciationPerYear
        };
    }
}
