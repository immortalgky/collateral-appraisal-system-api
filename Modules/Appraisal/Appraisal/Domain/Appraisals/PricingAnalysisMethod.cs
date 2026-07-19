using Appraisal.Domain.Appraisals.Hypothesis;
using Appraisal.Domain.Appraisals.Income;

namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Methods under each approach (WQS, SaleGrid, DirectComparison, etc.).
/// </summary>
public class PricingAnalysisMethod : Entity<Guid>
{
    private readonly List<PricingComparableLink> _comparableLinks = [];
    private readonly List<PricingCalculation> _calculations = [];
    private readonly List<PricingComparativeFactor> _comparativeFactors = [];
    private readonly List<PricingFactorScore> _factorScores = [];
    private readonly List<MachineCostItem> _machineCostItems = [];

    public IReadOnlyList<PricingComparableLink> ComparableLinks => _comparableLinks.AsReadOnly();
    public IReadOnlyList<PricingCalculation> Calculations => _calculations.AsReadOnly();
    public IReadOnlyList<PricingComparativeFactor> ComparativeFactors => _comparativeFactors.AsReadOnly();
    public IReadOnlyList<PricingFactorScore> FactorScores => _factorScores.AsReadOnly();
    public IReadOnlyList<MachineCostItem> MachineCostItems => _machineCostItems.AsReadOnly();

    public Guid ApproachId { get; private set; }
    public Guid? ComparativeAnalysisTemplateId { get; private set; }

    // Method
    public string MethodType { get; private set; } =
        null!; // WQS, SaleGrid, DirectComparison, CostApproach, DCF, CapitalizationRate

    public decimal? MethodValue { get; private set; }
    public decimal? ValuePerUnit { get; private set; }
    public string? UnitType { get; private set; } // PerSqWa, PerSqm, PerUnit (PerUnit = whole-unit lumpsum)
    public bool IsSelected { get; private set; }
    public string? Remark { get; private set; }

    // Final Value (1:1)
    public PricingFinalValue? FinalValue { get; private set; }

    // RSQ Result (1:1, WQS only)
    public PricingRsqResult? RsqResult { get; private set; }

    // Leasehold Analysis (1:1, Leasehold only)
    public LeaseholdAnalysis? LeaseholdAnalysis { get; private set; }

    // Profit Rent Analysis (1:1, ProfitRent only)
    public ProfitRentAnalysis? ProfitRentAnalysis { get; private set; }

    // Income Analysis (1:1, Income only)
    public IncomeAnalysis? IncomeAnalysis { get; private set; }

    // Hypothesis Analysis (1:1, Hypothesis — variant chosen at generate time)
    public HypothesisAnalysis? HypothesisAnalysis { get; private set; }

    private PricingAnalysisMethod()
    {
        // For EF Core
    }

    public static PricingAnalysisMethod Create(
        Guid approachId,
        string methodType,
        string status = "Selected")
    {
        var validMethods = new[] { "WQS", "SaleGrid", "DirectComparison", "MachineryCost", "BuildingCost", "Income", "Leasehold", "ProfitRent", "Hypothesis" };
        if (!validMethods.Contains(methodType))
            throw new ArgumentException($"MethodType must be one of: {string.Join(", ", validMethods)}");

        if (status != "Selected" && status != "Alternative")
            throw new ArgumentException("Status must be 'Selected' or 'Alternative'");

        return new PricingAnalysisMethod
        {
            Id = Guid.CreateVersion7(),
            ApproachId = approachId,
            MethodType = methodType,
            IsSelected = false
        };
    }

    /// <summary>
    /// Deep-clone for CI carry-forward — copies all scalars, every child collection, and all 1:1 method analyses.
    /// <paramref name="propertyIdMap"/> remaps prior AppraisalPropertyId → new id on MachineCostItems
    /// (caller passes the prior→new property map built during property copy). Items whose property
    /// is unmapped are dropped.
    /// </summary>
    public static PricingAnalysisMethod CloneForApproach(
        PricingAnalysisMethod source,
        Guid newApproachId,
        IReadOnlyDictionary<Guid, Guid>? propertyIdMap = null)
    {
        var clone = new PricingAnalysisMethod
        {
            Id = Guid.CreateVersion7(),
            ApproachId = newApproachId,
            ComparativeAnalysisTemplateId = source.ComparativeAnalysisTemplateId,
            MethodType = source.MethodType,
            MethodValue = source.MethodValue,
            ValuePerUnit = source.ValuePerUnit,
            UnitType = source.UnitType,
            IsSelected = source.IsSelected,
            Remark = source.Remark
        };

        foreach (var l in source.ComparableLinks)
            clone._comparableLinks.Add(PricingComparableLink.CloneForMethod(l, clone.Id));

        foreach (var c in source.Calculations)
            clone._calculations.Add(PricingCalculation.CloneForMethod(c, clone.Id));

        foreach (var f in source.ComparativeFactors)
            clone._comparativeFactors.Add(PricingComparativeFactor.CloneForMethod(f, clone.Id));

        foreach (var s in source.FactorScores)
            clone._factorScores.Add(PricingFactorScore.CloneForMethod(s, clone.Id));

        foreach (var mci in source.MachineCostItems)
        {
            if (propertyIdMap is not null
                && propertyIdMap.TryGetValue(mci.AppraisalPropertyId, out var newPropId))
            {
                clone._machineCostItems.Add(MachineCostItem.CloneForMethod(mci, clone.Id, newPropId));
            }
            // else: drop — no matching new property to attach to.
        }

        if (source.FinalValue is not null)
            clone.FinalValue = PricingFinalValue.CloneForMethod(source.FinalValue, clone.Id);

        if (source.RsqResult is not null)
            clone.RsqResult = PricingRsqResult.CloneForMethod(source.RsqResult, clone.Id);

        if (source.LeaseholdAnalysis is not null)
            clone.LeaseholdAnalysis = LeaseholdAnalysis.CloneForMethod(source.LeaseholdAnalysis, clone.Id);

        if (source.ProfitRentAnalysis is not null)
            clone.ProfitRentAnalysis = ProfitRentAnalysis.CloneForMethod(source.ProfitRentAnalysis, clone.Id);

        if (source.IncomeAnalysis is not null)
            clone.IncomeAnalysis = Income.IncomeAnalysis.CloneForMethod(source.IncomeAnalysis, clone.Id);

        if (source.HypothesisAnalysis is not null)
            clone.HypothesisAnalysis = Hypothesis.HypothesisAnalysis.CloneForMethod(source.HypothesisAnalysis, clone.Id);

        return clone;
    }

    public PricingComparableLink LinkComparable(Guid marketComparableId, int displaySequence, decimal? weight = null)
    {
        if (_comparableLinks.Any(c => c.MarketComparableId == marketComparableId))
            throw new InvalidOperationException("Market comparable already linked");

        var link = PricingComparableLink.Create(Id, marketComparableId, displaySequence, weight);
        _comparableLinks.Add(link);
        return link;
    }

    public PricingCalculation AddCalculation(Guid marketComparableId)
    {
        var calculation = PricingCalculation.Create(Id, marketComparableId);
        _calculations.Add(calculation);
        return calculation;
    }

    public void SetValue(decimal value, decimal? valuePerUnit = null, string? unitType = null)
    {
        MethodValue = value;
        ValuePerUnit = valuePerUnit;
        UnitType = unitType;
    }

    public void SetComparativeAnalysisTemplate(Guid? templateId)
    {
        ComparativeAnalysisTemplateId = templateId;
    }

    public void SetRemark(string? remark)
    {
        Remark = remark;
    }

    public void SetAsSelected()
    {
        IsSelected = true;
    }

    public void SetAsUnselected()
    {
        IsSelected = false;
    }

    public void SetFinalValue(PricingFinalValue finalValue)
    {
        FinalValue = finalValue;
    }

    public void SetRsqResult(PricingRsqResult rsqResult)
    {
        RsqResult = rsqResult;
    }

    public void SetLeaseholdAnalysis(LeaseholdAnalysis analysis)
    {
        LeaseholdAnalysis = analysis;
    }

    public void ClearLeaseholdAnalysis()
    {
        LeaseholdAnalysis = null;
    }

    public void SetProfitRentAnalysis(ProfitRentAnalysis analysis)
    {
        ProfitRentAnalysis = analysis;
    }

    public void ClearProfitRentAnalysis()
    {
        ProfitRentAnalysis = null;
    }

    public void SetIncomeAnalysis(IncomeAnalysis analysis)
    {
        IncomeAnalysis = analysis;
    }

    public void ClearIncomeAnalysis()
    {
        IncomeAnalysis = null;
    }

    public void SetHypothesisAnalysis(HypothesisAnalysis analysis)
    {
        HypothesisAnalysis = analysis;
    }

    public void ClearHypothesisAnalysis()
    {
        HypothesisAnalysis = null;
    }

    /// <summary>
    /// Removes a comparable link from this method
    /// </summary>
    public void RemoveComparableLink(Guid linkId)
    {
        var link = _comparableLinks.FirstOrDefault(l => l.Id == linkId);
        if (link is null)
            throw new InvalidOperationException($"Comparable link with ID {linkId} not found.");

        _comparableLinks.Remove(link);
    }

    /// <summary>
    /// Removes a calculation by market-comparable ID
    /// </summary>
    public void RemoveCalculationByComparableId(Guid marketComparableId)
    {
        var calculation = _calculations.FirstOrDefault(c => c.MarketComparableId == marketComparableId);
        if (calculation is not null) _calculations.Remove(calculation);
    }

    /// <summary>
    /// Removes all factor scores for a specific market comparable
    /// </summary>
    public void RemoveFactorScoresByComparableId(Guid marketComparableId)
    {
        var scores = _factorScores.Where(f => f.MarketComparableId == marketComparableId).ToList();
        foreach (var score in scores) _factorScores.Remove(score);
    }

    #region Comparative Factor Methods (Step 1)

    /// <summary>
    /// Adds a comparative factor for Step 1 factor selection.
    /// </summary>
    public PricingComparativeFactor AddComparativeFactor(
        Guid factorId,
        int displaySequence,
        bool isSelectedForScoring = false,
        string? remarks = null,
        string? collateralValue = null)
    {
        if (_comparativeFactors.Any(f => f.FactorId == factorId))
            throw new InvalidOperationException($"Factor {factorId} already exists in comparative factors");

        var factor = PricingComparativeFactor.Create(Id, factorId, displaySequence, isSelectedForScoring, remarks, collateralValue);
        _comparativeFactors.Add(factor);
        return factor;
    }

    /// <summary>
    /// Gets a comparative factor by ID
    /// </summary>
    public PricingComparativeFactor? GetComparativeFactor(Guid id)
    {
        return _comparativeFactors.FirstOrDefault(f => f.Id == id);
    }

    /// <summary>
    /// Removes a comparative factor by ID
    /// </summary>
    public void RemoveComparativeFactor(Guid id)
    {
        var factor = _comparativeFactors.FirstOrDefault(f => f.Id == id);
        if (factor is not null)
            _comparativeFactors.Remove(factor);
    }

    /// <summary>
    /// Clears all comparative factors
    /// </summary>
    public void ClearComparativeFactors()
    {
        _comparativeFactors.Clear();
    }

    #endregion

    #region Factor Score Methods (Step 2)

    /// <summary>
    /// Adds a factor score for Step 2 scoring
    /// </summary>
    public PricingFactorScore AddFactorScore(
        Guid factorId,
        decimal factorWeight,
        int displaySequence,
        Guid? marketComparableId = null)
    {
        // Allow same factor for different comparables
        if (_factorScores.Any(f => f.FactorId == factorId && f.MarketComparableId == marketComparableId))
            throw new InvalidOperationException(
                $"Factor {factorId} for comparable {marketComparableId} already exists");

        var score = PricingFactorScore.Create(Id, factorId, factorWeight, displaySequence, marketComparableId);
        _factorScores.Add(score);
        return score;
    }

    /// <summary>
    /// Gets a factor score by ID
    /// </summary>
    public PricingFactorScore? GetFactorScore(Guid id)
    {
        return _factorScores.FirstOrDefault(f => f.Id == id);
    }

    /// <summary>
    /// Removes a factor score by ID
    /// </summary>
    public void RemoveFactorScore(Guid id)
    {
        var score = _factorScores.FirstOrDefault(f => f.Id == id);
        if (score is not null)
            _factorScores.Remove(score);
    }

    /// <summary>
    /// Clears all factor scores
    /// </summary>
    public void ClearFactorScores()
    {
        _factorScores.Clear();
    }

    /// <summary>
    /// Clears all calculations
    /// </summary>
    public void ClearCalculations()
    {
        _calculations.Clear();
    }

    /// <summary>
    /// Clears all comparable links
    /// </summary>
    public void ClearComparableLinks()
    {
        _comparableLinks.Clear();
    }

    #region Machine Cost Item Methods

    public MachineCostItem AddMachineCostItem(Guid appraisalPropertyId, int displaySequence)
    {
        if (_machineCostItems.Any(i => i.AppraisalPropertyId == appraisalPropertyId))
            throw new InvalidOperationException("Machine cost item already exists for this property");

        var item = MachineCostItem.Create(Id, appraisalPropertyId, displaySequence);
        _machineCostItems.Add(item);
        return item;
    }

    public void RemoveMachineCostItem(Guid itemId)
    {
        var item = _machineCostItems.FirstOrDefault(i => i.Id == itemId);
        if (item is not null)
            _machineCostItems.Remove(item);
    }

    public void ClearMachineCostItems()
    {
        _machineCostItems.Clear();
    }

    #endregion

    /// <summary>
    /// Resets all method data: factors, scores, calculations, links, final value, and RSQ result
    /// </summary>
    public void Reset()
    {
        ClearComparativeFactors();
        ClearFactorScores();
        ClearCalculations();
        ClearComparableLinks();
        ClearMachineCostItems();
        FinalValue = null;
        RsqResult = null;
        ClearLeaseholdAnalysis();
        ClearProfitRentAnalysis();
        ClearIncomeAnalysis();
        ClearHypothesisAnalysis();
        MethodValue = null;
        ValuePerUnit = null;
        UnitType = null;
        IsSelected = false;
    }

    /// <summary>
    /// Gets factor scores for a specific comparable (or collateral if null)
    /// </summary>
    public IEnumerable<PricingFactorScore> GetFactorScoresForComparable(Guid? marketComparableId)
    {
        return _factorScores.Where(f => f.MarketComparableId == marketComparableId)
            .OrderBy(f => f.DisplaySequence);
    }

    #endregion
}