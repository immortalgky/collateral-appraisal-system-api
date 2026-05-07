namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Growth rate period for leasehold land value calculation (Period mode).
/// </summary>
public class LeaseholdLandGrowthPeriod : Entity<Guid>
{
    public Guid LeaseholdAnalysisId { get; private set; }
    public int FromYear { get; private set; }
    public int ToYear { get; private set; }
    public decimal GrowthRatePercent { get; private set; }

    private LeaseholdLandGrowthPeriod() { }

    public static LeaseholdLandGrowthPeriod Create(
        Guid leaseholdAnalysisId,
        int fromYear,
        int toYear,
        decimal growthRatePercent)
    {
        return new LeaseholdLandGrowthPeriod
        {
            Id = Guid.CreateVersion7(),
            LeaseholdAnalysisId = leaseholdAnalysisId,
            FromYear = fromYear,
            ToYear = toYear,
            GrowthRatePercent = growthRatePercent
        };
    }

    /// <summary>Deep-clone for CI carry-forward.</summary>
    public static LeaseholdLandGrowthPeriod CloneForAnalysis(LeaseholdLandGrowthPeriod source, Guid newAnalysisId)
    {
        return new LeaseholdLandGrowthPeriod
        {
            Id = Guid.CreateVersion7(),
            LeaseholdAnalysisId = newAnalysisId,
            FromYear = source.FromYear,
            ToYear = source.ToYear,
            GrowthRatePercent = source.GrowthRatePercent
        };
    }
}
