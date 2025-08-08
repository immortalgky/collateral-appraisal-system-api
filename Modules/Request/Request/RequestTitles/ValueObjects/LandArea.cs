namespace Request.RequestTitles.ValueObjects;

public class LandArea : ValueObject
{
    public int? Rai { get; }
    public int? Ngan { get; }
    public decimal? Wa { get; }

    private LandArea(int? rai, int? ngan, decimal? wa)
    {
        Rai = rai;
        Ngan = ngan;
        Wa = wa;
    }

    public static LandArea Of(int? rai, int? ngan, decimal? wa)
    {
        return new LandArea(rai, ngan, wa);
    }

    public static LandArea Zero => new(0, 0, 0);

    public decimal ToTotalWa()
    {
        var raiInWa = (Rai ?? 0) * 400m;
        var nganInWa = (Ngan ?? 0) * 100m;
        var wa = Wa ?? 0m;
        return raiInWa + nganInWa + wa;
    }

    public decimal ToSquareMeters()
    {
        return ToTotalWa() * 4m;
    }

    public LandArea Add(LandArea other)
    {
        if (other == null) return this;
        
        var totalWa = ToTotalWa() + other.ToTotalWa();
        return FromTotalWa(totalWa);
    }

    public LandArea Subtract(LandArea other)
    {
        if (other == null) return this;
        
        var totalWa = Math.Max(0, ToTotalWa() - other.ToTotalWa());
        return FromTotalWa(totalWa);
    }

    public int CompareTo(LandArea other)
    {
        if (other == null) return 1;
        return ToTotalWa().CompareTo(other.ToTotalWa());
    }

    public bool IsGreaterThan(LandArea other)
    {
        return CompareTo(other) > 0;
    }

    public bool IsLessThan(LandArea other)
    {
        return CompareTo(other) < 0;
    }

    public override bool IsEmpty()
    {
        return ToTotalWa() == 0;
    }

    public bool IsSignificant(decimal minimumWa = 1m)
    {
        return ToTotalWa() >= minimumWa;
    }

    public bool IsValid()
    {
        return (Rai ?? 0) >= 0 && (Ngan ?? 0) >= 0 && (Wa ?? 0) >= 0;
    }

    private static LandArea FromTotalWa(decimal totalWa)
    {
        var rai = (int)(totalWa / 400);
        var remainingWa = totalWa % 400;
        var ngan = (int)(remainingWa / 100);
        var wa = remainingWa % 100;

        return new LandArea(
            rai > 0 ? rai : null,
            ngan > 0 ? ngan : null,
            wa > 0 ? wa : null
        );
    }

    public override string ToString()
    {
        var parts = new List<string>();
        
        if (Rai > 0) parts.Add($"{Rai} ไร่");
        if (Ngan > 0) parts.Add($"{Ngan} งาน");
        if (Wa > 0) parts.Add($"{Wa:F2} ตร.ว.");
        
        return parts.Count > 0 ? string.Join(" ", parts) : "0 ตร.ว.";
    }
}