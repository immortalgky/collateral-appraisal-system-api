namespace Appraisal.Domain.Appraisals.Hypothesis.CostItems;

/// <summary>
/// Valid values for <see cref="HypothesisCostItem.DepreciationMethod"/>.
/// Stored as a string column (max 16) to match the Status/PaymentStatus convention used elsewhere.
/// </summary>
public static class DepreciationMethod
{
    /// <summary>
    /// B06 = min(100, Year × AnnualDepreciationPercent). Default behaviour.
    /// </summary>
    public const string Gross = "Gross";

    /// <summary>
    /// B06 = min(100, Σ (ToYear − AtYear + 1) × DepreciationPerYear) over DepreciationPeriods.
    /// </summary>
    public const string Period = "Period";

    private static readonly HashSet<string> Valid = [Gross, Period];

    /// <returns>True when <paramref name="value"/> is "Gross" or "Period".</returns>
    public static bool IsValid(string? value) => value is not null && Valid.Contains(value);
}

/// <summary>
/// A single cost row for the Hypothesis pricing analysis.
/// Used for all cost sections across both variants.
/// </summary>
public class HypothesisCostItem : Entity<Guid>
{
    private readonly List<HypothesisCostItemDepreciationPeriod> _depreciationPeriods = [];

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

    // ── CostOfBuilding categorisation ─────────────────────────────────────
    /// <summary>
    /// Flags this row as a "Building" component (true) vs a non-building component (false).
    /// Used by the FE to group rows within a model section.
    /// Only meaningful when Category == CostOfBuilding.
    /// </summary>
    public bool IsBuilding { get; private set; } = true;

    /// <summary>
    /// Controls how the total depreciation percent (B06) is calculated.
    /// "Gross" (default): B06 = min(100, B04 × B05).
    /// "Period": B06 = min(100, Σ (ToYear − AtYear + 1) × DepreciationPerYear).
    /// Only meaningful when Category == CostOfBuilding.
    /// </summary>
    public string DepreciationMethod { get; private set; } = CostItems.DepreciationMethod.Gross;

    /// <summary>
    /// Child period rows used when <see cref="DepreciationMethod"/> == "Period".
    /// Ordered by <see cref="HypothesisCostItemDepreciationPeriod.Sequence"/>.
    /// </summary>
    public IReadOnlyCollection<HypothesisCostItemDepreciationPeriod> DepreciationPeriods
        => _depreciationPeriods.AsReadOnly();

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

    // ── CostOfBuilding-specific B-fields (FSD §2.1.3.5.1 Figure 52) ──────
    // Null on non-CostOfBuilding rows.

    /// <summary>
    /// Area of this building component in square metres.
    /// FSD B01 — user input.
    /// </summary>
    public decimal? Area { get; private set; }

    /// <summary>
    /// Construction cost per square metre (Baht/SqM).
    /// FSD B02 — user input.
    /// </summary>
    public decimal? PricePerSqM { get; private set; }

    /// <summary>
    /// Construction cost before depreciation = Area × PricePerSqM.
    /// FSD B03 — computed/stored.
    /// </summary>
    public decimal? PriceBeforeDepreciation { get; private set; }

    /// <summary>
    /// Age of the building in years.
    /// FSD B04 — user input.
    /// </summary>
    public int? Year { get; private set; }

    /// <summary>
    /// Annual depreciation rate in percent per year.
    /// FSD B05 — user input.
    /// </summary>
    public decimal? AnnualDepreciationPercent { get; private set; }

    /// <summary>
    /// Cumulative depreciation percent.
    /// Gross: min(100, Year × AnnualDepreciationPercent).
    /// Period: min(100, Σ (ToYear − AtYear + 1) × DepreciationPerYear).
    /// FSD B06 — computed/stored.
    /// </summary>
    public decimal? TotalDepreciationPercent { get; private set; }

    /// <summary>
    /// Depreciation amount in Baht = PriceBeforeDepreciation × TotalDepreciationPercent / 100.
    /// FSD B07 — computed/stored.
    /// </summary>
    public decimal? DepreciationAmount { get; private set; }

    /// <summary>
    /// Value after depreciation = PriceBeforeDepreciation − DepreciationAmount.
    /// FSD B08 — computed/stored.
    /// </summary>
    public decimal? ValueAfterDepreciation { get; private set; }

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

    /// <summary>
    /// Sets the user-supplied B01/B02/B04/B05 inputs for a CostOfBuilding row,
    /// plus the IsBuilding flag, DepreciationMethod, and the Period collection.
    /// Null values for B01/B02/B04/B05 are stored as-is (preserves "user hasn't entered yet" state).
    /// Only valid on CostOfBuilding rows.
    /// </summary>
    /// <param name="depreciationPeriods">
    /// When <paramref name="depreciationMethod"/> is "Period", the ordered period tuples.
    /// Pass null or empty for "Gross" mode (periods are cleared regardless).
    /// </param>
    /// <exception cref="InvalidOperationException">Called on a non-CostOfBuilding row.</exception>
    /// <exception cref="ArgumentException">Invalid depreciationMethod or out-of-range period values.</exception>
    public void SetBuildingCostInputs(
        decimal? area,
        decimal? pricePerSqM,
        int? year,
        decimal? annualDepreciationPercent,
        bool isBuilding = true,
        string depreciationMethod = CostItems.DepreciationMethod.Gross,
        IReadOnlyList<(int AtYear, int ToYear, decimal DepreciationPerYear)>? depreciationPeriods = null)
    {
        if (Category != HypothesisCostCategory.CostOfBuilding)
            throw new InvalidOperationException(
                "SetBuildingCostInputs is only valid for CostOfBuilding items.");

        if (!CostItems.DepreciationMethod.IsValid(depreciationMethod))
            throw new ArgumentException(
                $"DepreciationMethod must be '{CostItems.DepreciationMethod.Gross}' or '{CostItems.DepreciationMethod.Period}'.",
                nameof(depreciationMethod));

        // Validate periods
        if (depreciationPeriods is not null)
        {
            foreach (var (atYear, toYear, depPerYear) in depreciationPeriods)
            {
                if (atYear < 0)
                    throw new ArgumentException($"Period AtYear must be >= 0 (got {atYear}).", nameof(depreciationPeriods));
                if (toYear < atYear)
                    throw new ArgumentException($"Period ToYear ({toYear}) must be >= AtYear ({atYear}).", nameof(depreciationPeriods));
                if (depPerYear < 0m || depPerYear > 100m)
                    throw new ArgumentException($"Period DepreciationPerYear must be in [0, 100] (got {depPerYear}).", nameof(depreciationPeriods));
            }
        }

        Area = area;
        PricePerSqM = pricePerSqM;
        Year = year;
        AnnualDepreciationPercent = annualDepreciationPercent;
        IsBuilding = isBuilding;
        DepreciationMethod = depreciationMethod;

        // Full-replace the period collection
        _depreciationPeriods.Clear();
        if (depreciationPeriods is not null)
        {
            int seq = 0;
            foreach (var (atYear, toYear, depPerYear) in depreciationPeriods)
            {
                _depreciationPeriods.Add(
                    HypothesisCostItemDepreciationPeriod.Create(Id, seq++, atYear, toYear, depPerYear));
            }
        }
    }

    /// <summary>
    /// Stores the server-computed B03/B06/B07/B08 fields.
    /// Called by the calculation service after computing from inputs.
    /// </summary>
    public void SetBuildingCostComputedFields(
        decimal? priceBeforeDepreciation,
        decimal? totalDepreciationPercent,
        decimal? depreciationAmount,
        decimal? valueAfterDepreciation)
    {
        PriceBeforeDepreciation = priceBeforeDepreciation;
        TotalDepreciationPercent = totalDepreciationPercent;
        DepreciationAmount = depreciationAmount;
        ValueAfterDepreciation = valueAfterDepreciation;
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
