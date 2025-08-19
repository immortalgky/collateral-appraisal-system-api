namespace Collateral.CollateralProperties.ValueObjects;

public class DecorationDetail : ValueObject
{
    public string Decoration { get; } = default!;
    public string? DecorationOther { get; }

    private DecorationDetail(string decoration, string? decorationOther)
    {
        Decoration = decoration;
        DecorationOther = decorationOther;
    }

    public static DecorationDetail Create(string decoration, string? decorationOther)
    {
        return new DecorationDetail(decoration, decorationOther);
    }
}
