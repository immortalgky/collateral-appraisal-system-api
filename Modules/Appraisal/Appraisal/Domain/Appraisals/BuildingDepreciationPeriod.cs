namespace Appraisal.Domain.Appraisals;

/// <summary>
/// A year-range depreciation period within a BuildingDepreciationDetail.
/// Used when AppraisalMethod = "Period".
/// </summary>
public class BuildingDepreciationPeriod : Entity<Guid>
{
    public Guid BuildingDepreciationDetailId { get; private set; }

    public int AtYear { get; private set; }
    public int ToYear { get; private set; }
    public decimal DepreciationPerYear { get; private set; }
    public decimal TotalDepreciationPct { get; private set; }
    public decimal PriceDepreciation { get; private set; }

    private BuildingDepreciationPeriod()
    {
    }

    public static BuildingDepreciationPeriod Create(
        Guid buildingDepreciationDetailId,
        int atYear,
        int toYear,
        decimal depreciationPerYear,
        decimal totalDepreciationPct,
        decimal priceDepreciation)
    {
        return new BuildingDepreciationPeriod
        {
            //Id = Guid.CreateVersion7(),
            BuildingDepreciationDetailId = buildingDepreciationDetailId,
            AtYear = atYear,
            ToYear = toYear,
            DepreciationPerYear = depreciationPerYear,
            TotalDepreciationPct = totalDepreciationPct,
            PriceDepreciation = priceDepreciation
        };
    }
}