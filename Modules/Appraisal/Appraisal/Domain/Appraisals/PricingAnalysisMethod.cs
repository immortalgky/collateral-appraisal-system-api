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

    public IReadOnlyList<PricingComparableLink> ComparableLinks => _comparableLinks.AsReadOnly();
    public IReadOnlyList<PricingCalculation> Calculations => _calculations.AsReadOnly();
    public IReadOnlyList<PricingComparativeFactor> ComparativeFactors => _comparativeFactors.AsReadOnly();
    public IReadOnlyList<PricingFactorScore> FactorScores => _factorScores.AsReadOnly();

    public Guid ApproachId { get; private set; }

    // Method
    public string MethodType { get; private set; } =
        null!; // WQS, SaleGrid, DirectComparison, CostApproach, DCF, CapitalizationRate
    public decimal? MethodValue { get; private set; }
    public decimal? ValuePerUnit { get; private set; }
    public string? UnitType { get; private set; } // Sqm, Rai, Unit
    public bool IsSelected { get; private set; }

    // Final Value (1:1)
    public PricingFinalValue? FinalValue { get; private set; }

    private PricingAnalysisMethod()
    {
        // For EF Core
    }

    public static PricingAnalysisMethod Create(
        Guid approachId,
        string methodType,
        string status = "Selected")
    {
        var validMethods = new[] { "WQS", "SaleGrid", "DirectComparison", "CostApproach", "DCF", "CapitalizationRate" };
        if (!validMethods.Contains(methodType))
            throw new ArgumentException($"MethodType must be one of: {string.Join(", ", validMethods)}");

        if (status != "Selected" && status != "Alternative")
            throw new ArgumentException("Status must be 'Selected' or 'Alternative'");

        return new PricingAnalysisMethod
        {
            // Id = Guid.NewGuid(),
            ApproachId = approachId,
            MethodType = methodType,
            IsSelected = false
        };
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

    #region Comparative Factor Methods (Step 1)

    /// <summary>
    /// Adds a comparative factor for Step 1 factor selection
    /// </summary>
    public PricingComparativeFactor AddComparativeFactor(
        Guid factorId,
        int displaySequence,
        bool isSelectedForScoring = false,
        string? remarks = null)
    {
        if (_comparativeFactors.Any(f => f.FactorId == factorId))
            throw new InvalidOperationException($"Factor {factorId} already exists in comparative factors");

        var factor = PricingComparativeFactor.Create(Id, factorId, displaySequence, isSelectedForScoring, remarks);
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
            throw new InvalidOperationException($"Factor {factorId} for comparable {marketComparableId} already exists");

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
    /// Gets factor scores for a specific comparable (or collateral if null)
    /// </summary>
    public IEnumerable<PricingFactorScore> GetFactorScoresForComparable(Guid? marketComparableId)
    {
        return _factorScores.Where(f => f.MarketComparableId == marketComparableId)
            .OrderBy(f => f.DisplaySequence);
    }

    #endregion
}
