namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Per-machine cost calculation item within a MachineryCost pricing method.
/// Each item references an AppraisalProperty (MAC type) and stores depreciation inputs + computed FMV.
/// </summary>
public class MachineCostItem : Entity<Guid>
{
    public Guid PricingMethodId { get; private set; }
    public Guid AppraisalPropertyId { get; private set; }
    public int DisplaySequence { get; private set; }

    // Input fields
    public decimal? RcnReplacementCost { get; private set; }
    public decimal? LifeSpanYears { get; private set; } // N
    public decimal ConditionFactor { get; private set; } // C
    public decimal FunctionalObsolescence { get; private set; } // F
    public decimal EconomicObsolescence { get; private set; } // E

    // Computed field (set by calculation service on save)
    public decimal? FairMarketValue { get; private set; } // FMV

    public bool MarketDemandAvailable { get; private set; }
    public string? Notes { get; private set; }

    private MachineCostItem()
    {
    }

    public static MachineCostItem Create(
        Guid pricingMethodId,
        Guid appraisalPropertyId,
        int displaySequence)
    {
        return new MachineCostItem
        {
            Id = Guid.CreateVersion7(),
            PricingMethodId = pricingMethodId,
            AppraisalPropertyId = appraisalPropertyId,
            DisplaySequence = displaySequence,
            ConditionFactor = 0m,
            FunctionalObsolescence = 1m,
            EconomicObsolescence = 1m
        };
    }

    /// <summary>
    /// Deep-clone for CI carry-forward. AppraisalPropertyId must be the NEW property's id —
    /// caller maps prior property → new property using the priorToNewProperties dictionary built
    /// during property copy.
    /// </summary>
    public static MachineCostItem CloneForMethod(MachineCostItem source, Guid newMethodId, Guid newAppraisalPropertyId)
    {
        return new MachineCostItem
        {
            Id = Guid.CreateVersion7(),
            PricingMethodId = newMethodId,
            AppraisalPropertyId = newAppraisalPropertyId,
            DisplaySequence = source.DisplaySequence,
            RcnReplacementCost = source.RcnReplacementCost,
            LifeSpanYears = source.LifeSpanYears,
            ConditionFactor = source.ConditionFactor,
            FunctionalObsolescence = source.FunctionalObsolescence,
            EconomicObsolescence = source.EconomicObsolescence,
            FairMarketValue = source.FairMarketValue,
            MarketDemandAvailable = source.MarketDemandAvailable,
            Notes = source.Notes
        };
    }

    public void Update(
        decimal? rcnReplacementCost,
        decimal? lifeSpanYears,
        decimal conditionFactor,
        decimal functionalObsolescence,
        decimal economicObsolescence,
        bool marketDemandAvailable,
        string? notes,
        int displaySequence)
    {
        RcnReplacementCost = rcnReplacementCost;
        LifeSpanYears = lifeSpanYears;
        ConditionFactor = conditionFactor;
        FunctionalObsolescence = functionalObsolescence;
        EconomicObsolescence = economicObsolescence;
        MarketDemandAvailable = marketDemandAvailable;
        Notes = notes;
        DisplaySequence = displaySequence;
    }

    public void SetFairMarketValue(decimal? fmv)
    {
        FairMarketValue = fmv;
    }
}
