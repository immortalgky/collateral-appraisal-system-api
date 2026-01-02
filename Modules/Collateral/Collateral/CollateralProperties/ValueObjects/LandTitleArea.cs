namespace Collateral.CollateralProperties.ValueObjects;

public class LandTitleArea : ValueObject
{
    public decimal? Rai { get; }
    public decimal? Ngan { get; }
    public decimal? Wa { get; }
    public decimal? TotalAreaInSqWa { get; }

    private LandTitleArea(decimal? rai, decimal? ngan, decimal? wa, decimal? totalAreaInSqWa)
    {
        Rai = rai;
        Ngan = ngan;
        Wa = wa;
        TotalAreaInSqWa = totalAreaInSqWa;
    }

    public static LandTitleArea Create(
        decimal? rai,
        decimal? ngan,
        decimal? wa,
        decimal? totalAreaInSqWa
    )
    {
        return new LandTitleArea(rai, ngan, wa, totalAreaInSqWa);
    }
}
