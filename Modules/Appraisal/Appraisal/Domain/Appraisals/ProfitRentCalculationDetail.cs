namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Stores a single row of the profit rent calculation table.
/// Child of ProfitRentAnalysis — regenerated on every save.
/// </summary>
public class ProfitRentCalculationDetail : Entity<Guid>
{
    public Guid ProfitRentAnalysisId { get; private set; }
    public int DisplaySequence { get; private set; }
    public decimal Year { get; private set; }
    public decimal NumberOfMonths { get; private set; }
    public decimal MarketRentalFeePerSqWa { get; private set; }
    public decimal MarketRentalFeeGrowthPercent { get; private set; }
    public decimal MarketRentalFeePerMonth { get; private set; }
    public decimal MarketRentalFeePerYear { get; private set; }
    public decimal ContractRentalFeePerYear { get; private set; }
    public decimal ReturnsFromLease { get; private set; }
    public decimal PvFactor { get; private set; }
    public decimal PresentValue { get; private set; }

    private ProfitRentCalculationDetail() { }

    public static ProfitRentCalculationDetail Create(
        Guid profitRentAnalysisId,
        int displaySequence,
        decimal year,
        decimal numberOfMonths,
        decimal marketRentalFeePerSqWa,
        decimal marketRentalFeeGrowthPercent,
        decimal marketRentalFeePerMonth,
        decimal marketRentalFeePerYear,
        decimal contractRentalFeePerYear,
        decimal returnsFromLease,
        decimal pvFactor,
        decimal presentValue)
    {
        return new ProfitRentCalculationDetail
        {
            ProfitRentAnalysisId = profitRentAnalysisId,
            DisplaySequence = displaySequence,
            Year = year,
            NumberOfMonths = numberOfMonths,
            MarketRentalFeePerSqWa = marketRentalFeePerSqWa,
            MarketRentalFeeGrowthPercent = marketRentalFeeGrowthPercent,
            MarketRentalFeePerMonth = marketRentalFeePerMonth,
            MarketRentalFeePerYear = marketRentalFeePerYear,
            ContractRentalFeePerYear = contractRentalFeePerYear,
            ReturnsFromLease = returnsFromLease,
            PvFactor = pvFactor,
            PresentValue = presentValue
        };
    }
}
