namespace Appraisal.Domain.Appraisals;

/// <summary>
/// A year-range depreciation period within a VillageModelDepreciationDetail.
/// Used when DepreciationMethod = "Period".
/// </summary>
public class VillageModelDepreciationPeriod : Entity<Guid>
{
    public Guid VillageModelDepreciationDetailId { get; private set; }

    public int AtYear { get; private set; }
    public int ToYear { get; private set; }
    public decimal DepreciationPerYear { get; private set; }
    public decimal TotalDepreciationPct { get; private set; }
    public decimal PriceDepreciation { get; private set; }

    private VillageModelDepreciationPeriod()
    {
    }

    public static VillageModelDepreciationPeriod Create(
        Guid villageModelDepreciationDetailId,
        int atYear,
        int toYear,
        decimal depreciationPerYear,
        decimal totalDepreciationPct,
        decimal priceDepreciation)
    {
        return new VillageModelDepreciationPeriod
        {
            VillageModelDepreciationDetailId = villageModelDepreciationDetailId,
            AtYear = atYear,
            ToYear = toYear,
            DepreciationPerYear = depreciationPerYear,
            TotalDepreciationPct = totalDepreciationPct,
            PriceDepreciation = priceDepreciation
        };
    }
}
