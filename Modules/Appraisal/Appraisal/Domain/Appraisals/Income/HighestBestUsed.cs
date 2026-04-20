namespace Appraisal.Domain.Appraisals.Income;

/// <summary>
/// Owned value object on IncomeAnalysis.
/// Stores the user-entered land area + per-sq-wa price used to top up the final value
/// when the analyst determines the current income-approach valuation is not the
/// highest-and-best use of the land. Derived totals (TotalWa, TotalValue) are
/// recomputed on the client.
/// </summary>
public class HighestBestUsed
{
    public int? AreaRai { get; private set; }
    public int? AreaNgan { get; private set; }
    public decimal? AreaWa { get; private set; }
    public decimal? PricePerSqWa { get; private set; }

    private HighestBestUsed()
    {
        // For EF Core owned entity
    }

    public static HighestBestUsed Empty() => new();

    public static HighestBestUsed Create(
        int? areaRai,
        int? areaNgan,
        decimal? areaWa,
        decimal? pricePerSqWa)
    {
        return new HighestBestUsed
        {
            AreaRai = areaRai,
            AreaNgan = areaNgan,
            AreaWa = areaWa,
            PricePerSqWa = pricePerSqWa
        };
    }
}
