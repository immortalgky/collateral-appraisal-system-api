namespace Appraisal.Domain.Appraisals;

/// <summary>
/// A per-period growth rate entry within a RentalInfo (used in "Property" growth mode).
/// </summary>
public class RentalGrowthPeriodEntry : Entity<Guid>
{
    public Guid RentalInfoId { get; private set; }
    public int FromYear { get; private set; }
    public int ToYear { get; private set; }
    public decimal GrowthRate { get; private set; }
    public decimal GrowthAmount { get; private set; }
    public decimal TotalAmount { get; private set; }

    private RentalGrowthPeriodEntry()
    {
    }

    public static RentalGrowthPeriodEntry Create(
        Guid rentalInfoId,
        int fromYear,
        int toYear,
        decimal growthRate,
        decimal growthAmount,
        decimal totalAmount)
    {
        return new RentalGrowthPeriodEntry
        {
            RentalInfoId = rentalInfoId,
            FromYear = fromYear,
            ToYear = toYear,
            GrowthRate = growthRate,
            GrowthAmount = growthAmount,
            TotalAmount = totalAmount
        };
    }
}
