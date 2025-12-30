namespace Request.Domain.Requests;

public class Area : ValueObject
{
    private Area(
        decimal? rai,
        decimal? ngan,
        decimal? wa,
        decimal? usageArea
    )
    {
        Rai = rai;
        Ngan = ngan;
        Wa = wa;
        UsageArea = usageArea;
    }
    public decimal? Rai { get; }
    public decimal? Ngan { get; }
    public decimal? Wa { get; }
    public decimal? UsageArea { get; }

    public static Area Create(
        decimal? rai,
        decimal? ngan,
        decimal? wa,
        decimal? usageArea
    )
    {
        return new Area(rai, ngan, wa, usageArea);
    }
}