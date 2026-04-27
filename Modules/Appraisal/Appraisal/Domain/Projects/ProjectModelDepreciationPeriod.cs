namespace Appraisal.Domain.Projects;

/// <summary>
/// A year-range depreciation period within a ProjectModelDepreciationDetail.
/// Used when DepreciationMethod = "Period".
/// </summary>
public class ProjectModelDepreciationPeriod : Entity<Guid>
{
    public Guid ProjectModelDepreciationDetailId { get; private set; }

    public int AtYear { get; private set; }
    public int ToYear { get; private set; }
    public decimal DepreciationPerYear { get; private set; }
    public decimal TotalDepreciationPct { get; private set; }
    public decimal PriceDepreciation { get; private set; }

    private ProjectModelDepreciationPeriod()
    {
    }

    public static ProjectModelDepreciationPeriod Create(
        Guid projectModelDepreciationDetailId,
        int atYear,
        int toYear,
        decimal depreciationPerYear,
        decimal totalDepreciationPct,
        decimal priceDepreciation)
    {
        return new ProjectModelDepreciationPeriod
        {
            Id = Guid.CreateVersion7(),
            ProjectModelDepreciationDetailId = projectModelDepreciationDetailId,
            AtYear = atYear,
            ToYear = toYear,
            DepreciationPerYear = depreciationPerYear,
            TotalDepreciationPct = totalDepreciationPct,
            PriceDepreciation = priceDepreciation
        };
    }
}
