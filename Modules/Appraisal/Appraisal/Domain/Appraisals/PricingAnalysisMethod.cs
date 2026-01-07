namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Methods under each approach (WQS, SaleGrid, DirectComparison, etc.).
/// </summary>
public class PricingAnalysisMethod : Entity<Guid>
{
    private readonly List<PricingComparableLink> _comparableLinks = [];
    private readonly List<PricingCalculation> _calculations = [];

    public IReadOnlyList<PricingComparableLink> ComparableLinks => _comparableLinks.AsReadOnly();
    public IReadOnlyList<PricingCalculation> Calculations => _calculations.AsReadOnly();

    public Guid ApproachId { get; private set; }

    // Method
    public string MethodType { get; private set; } =
        null!; // WQS, SaleGrid, DirectComparison, CostApproach, DCF, CapitalizationRate

    public string Status { get; private set; } = null!; // Selected, Alternative
    public decimal? MethodValue { get; private set; }
    public decimal? ValuePerUnit { get; private set; }
    public string? UnitType { get; private set; } // Sqm, Rai, Unit

    // Final Value (1:1)
    public PricingFinalValue? FinalValue { get; private set; }

    private PricingAnalysisMethod()
    {
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
            Id = Guid.NewGuid(),
            ApproachId = approachId,
            MethodType = methodType,
            Status = status
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
        Status = "Selected";
    }

    public void SetAsAlternative()
    {
        Status = "Alternative";
    }

    public void SetFinalValue(PricingFinalValue finalValue)
    {
        FinalValue = finalValue;
    }
}