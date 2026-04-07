namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Growth rate period for profit rent market rental fee calculation (Period mode).
/// </summary>
public class ProfitRentGrowthPeriod : Entity<Guid>
{
    public Guid ProfitRentAnalysisId { get; private set; }
    public int FromYear { get; private set; }
    public int ToYear { get; private set; }
    public decimal GrowthRatePercent { get; private set; }

    private ProfitRentGrowthPeriod() { }

    public static ProfitRentGrowthPeriod Create(
        Guid profitRentAnalysisId,
        int fromYear,
        int toYear,
        decimal growthRatePercent)
    {
        return new ProfitRentGrowthPeriod
        {
            ProfitRentAnalysisId = profitRentAnalysisId,
            FromYear = fromYear,
            ToYear = toYear,
            GrowthRatePercent = growthRatePercent
        };
    }
}
