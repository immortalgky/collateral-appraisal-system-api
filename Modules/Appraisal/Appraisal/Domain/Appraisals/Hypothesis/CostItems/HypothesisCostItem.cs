namespace Appraisal.Domain.Appraisals.Hypothesis.CostItems;

/// <summary>
/// A single cost row for the Hypothesis pricing analysis.
/// Used for all cost sections across both variants.
/// </summary>
public class HypothesisCostItem : Entity<Guid>
{
    public Guid HypothesisAnalysisId { get; private set; }
    public HypothesisCostCategory Category { get; private set; }

    /// <summary>
    /// Stable semantic key used by the calculation service to locate this row.
    /// Description is a mutable UI label; Kind is immutable after creation.
    /// </summary>
    public CostItemKind Kind { get; private set; }

    /// <summary>
    /// Only populated when Category == CostOfBuilding (links to a specific house model).
    /// </summary>
    public string? ModelName { get; private set; }

    public string Description { get; private set; } = null!;
    public int DisplaySequence { get; private set; }

    // ── Primary amount fields ──────────────────────────────────────────────
    /// <summary>
    /// Per-unit rate (Baht/SqWa or Baht/Month or Baht/Unit, depending on item).
    /// Null for lump-sum items.
    /// </summary>
    public decimal? RateAmount { get; private set; }

    /// <summary>
    /// Quantity or area multiplier (units, months, sq.wa).
    /// Null for lump-sum items.
    /// </summary>
    public decimal? Quantity { get; private set; }

    /// <summary>
    /// Total amount for this cost item (Baht). Either entered directly or = Rate × Qty.
    /// </summary>
    public decimal Amount { get; private set; }

    /// <summary>
    /// Percentage of total revenue / total project cost (for % items like Selling/Adv, Transfer Fee).
    /// </summary>
    public decimal? RatePercent { get; private set; }

    /// <summary>
    /// Computed ratio of this item vs its category total (%).
    /// Populated by calculation service; not editable.
    /// </summary>
    public decimal? CategoryRatio { get; private set; }

    private HypothesisCostItem() { }

    public static HypothesisCostItem Create(
        Guid hypothesisAnalysisId,
        HypothesisCostCategory category,
        CostItemKind kind,
        string description,
        int displaySequence,
        string? modelName = null)
    {
        if (category == HypothesisCostCategory.CostOfBuilding && string.IsNullOrWhiteSpace(modelName))
            throw new ArgumentException("ModelName is required for CostOfBuilding items.");
        if (category != HypothesisCostCategory.CostOfBuilding && modelName is not null)
            throw new ArgumentException("ModelName must be null for non-CostOfBuilding items.");

        return new HypothesisCostItem
        {
            Id = Guid.CreateVersion7(),
            HypothesisAnalysisId = hypothesisAnalysisId,
            Category = category,
            Kind = kind,
            Description = description,
            DisplaySequence = displaySequence,
            ModelName = modelName
        };
    }

    /// <summary>
    /// Sets the monetary amounts for this cost item.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="amount"/> is negative.</exception>
    public void SetAmounts(decimal amount, decimal? rateAmount = null, decimal? quantity = null, decimal? ratePercent = null)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Cost item amount cannot be negative.");

        Amount = amount;
        RateAmount = rateAmount;
        Quantity = quantity;
        RatePercent = ratePercent;
    }

    public void SetCategoryRatio(decimal? ratio)
    {
        CategoryRatio = ratio;
    }

    public void UpdateDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description cannot be empty.");
        Description = description;
    }

    public void UpdateSequence(int sequence)
    {
        DisplaySequence = sequence;
    }
}
